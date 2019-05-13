using Amazon.Lambda;
using Amazon.Lambda.Core;
using Amazon.Lambda.Model;
using Amazon.S3;
using Amazon.S3.Transfer;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using BAMCIS.AWSLambda.Common;
using BAMCIS.AWSLambda.Common.Events;
using BAMCIS.AWSPriceListApi;
using BAMCIS.AWSPriceListApi.Serde;
using BAMCIS.LambdaFunctions.AWSPriceListReservedInstanceHelper.Models;
using CsvHelper;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace BAMCIS.LambdaFunctions.AWSPriceListReservedInstanceHelper
{
    /// <summary>
    /// The class that Lambda will call to invoke the Exec method
    /// </summary>
    public class Entrypoint
    {
        #region Private Fields

        private static ILambdaContext _context;
        private static PriceListClient priceListClient;
        private static IAmazonS3 s3Client;
        private static AmazonLambdaClient lambdaClient;
        private static IAmazonSimpleNotificationService snsClient;
        private static string snsTopic;
        private static string subject = "AWS RI Price List Helper Error";

        // Use the pipe so that commas in strings don't cause an issue
        private static readonly string defaultDelimiter = "|";

        #endregion

        #region Constructors

        static Entrypoint()
        {
            snsClient = new AmazonSimpleNotificationServiceClient();
            priceListClient = new PriceListClient();
            s3Client = new AmazonS3Client();
            lambdaClient = new AmazonLambdaClient();
            snsTopic = System.Environment.GetEnvironmentVariable("SNS");
        }

        /// <summary>
        /// Default constructor that Lambda will invoke.
        /// </summary>
        public Entrypoint()
        {
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Initiates a lambda function for each service that we want to get from the price list api
        /// </summary>
        /// <param name="ev"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public async Task LaunchWorkersAsync(CloudWatchScheduledEvent ev, ILambdaContext context)
        {
            _context = context;
            List<Task<InvokeResponse>> response = new List<Task<InvokeResponse>>();

            foreach (string service in Constants.ReservableServices)
            {
                try
                {
                    InvokeRequest Req = new InvokeRequest()
                    {
                        FunctionName = System.Environment.GetEnvironmentVariable("FunctionName"),
                        Payload = $"{{\"service\":\"{service}\"}}",
                        InvocationType = InvocationType.Event,
                        ClientContext = JsonConvert.SerializeObject(context.ClientContext, Formatting.None),
                    };

                    InvokeResponse lambdaResponse = await lambdaClient.InvokeAsync(Req);
                    context.LogInfo($"Completed kickoff for {service} with http status {(int)lambdaResponse.StatusCode}.");
                }
                catch (Exception e)
                {
                    context.LogError(e);
                    string message = $"[ERROR] {DateTime.Now} {{{context.AwsRequestId}}} : There was a problem creating a lambda invocation request for service {service} - {e.Message}";
                    await SNSNotify(message, context);
                    throw e;
                }
            }

            context.LogInfo("All kickoff requests completed.");
        }

        /// <summary>
        /// Executes the lambda function to get the price list data for the 
        /// set of services we can buy reserved instances for
        /// </summary>
        /// <param name="ev"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public async Task RunForServiceAsync(ServiceRequest req, ILambdaContext context)
        {
            _context = context;

            if (req == null || String.IsNullOrEmpty(req.Service))
            {
                string message = "No service was provided in the service request.";
                context.LogError(message);
                await SNSNotify(message, context);
                return;
            }

            // Get the product price data for the service
            context.LogInfo($"Getting product data for {req.Service}");

            string bucket = System.Environment.GetEnvironmentVariable("BUCKET");
            string delimiter = System.Environment.GetEnvironmentVariable("DELIMITER");
            string inputFormat = System.Environment.GetEnvironmentVariable("PRICELIST_FORMAT");

            if (String.IsNullOrEmpty(inputFormat))
            {
                inputFormat = "csv";
            }

            inputFormat = inputFormat.ToLower().Trim();

            context.LogInfo($"Using price list format: {inputFormat}");

            if (String.IsNullOrEmpty(delimiter))
            {
                delimiter = defaultDelimiter;
            }

            // This holds the disposable stream and writer objects
            // that need to be disposed at the end
            List<IDisposable> disposables = new List<IDisposable>();

            try
            {
                // Will hold the stream of price data content that the 
                // transfer utility will send
                MemoryStream memoryStreamOut = new MemoryStream();
                disposables.Add(memoryStreamOut);

                // Provided to the csv writer to write to the memory stream
                TextWriter streamWriter = new StreamWriter(memoryStreamOut);
                disposables.Add(streamWriter);

                // The csv writer to write the price data objects
                CsvWriter csvWriter = new CsvWriter(streamWriter);
                csvWriter.Configuration.Delimiter = delimiter;
                disposables.Add(csvWriter);

                // Write the header to the csv
                csvWriter.WriteHeader<ReservedInstancePricingTerm>();
                csvWriter.NextRecord();

                // Create the product request with the right format
                GetProductRequest productRequest = new GetProductRequest(req.Service)
                {
                    Format = inputFormat.Equals("json", StringComparison.OrdinalIgnoreCase) ? Format.JSON : Format.CSV
                };

                context.LogInfo("Getting price list offer file.");

                // Retrieve the finished get product price data response
                GetProductResponse response = await priceListClient.GetProductAsync(productRequest);

                context.LogInfo("Parsing price list data.");

                // Fill the output stream
                await this.FillOutputStreamWriter(response.ProductInfo, csvWriter, productRequest.Format);

                // Make sure everything is written out since we don't dispose
                // of these till later, if the textwriter isn't flushed
                // you will lose content from the csv file
                csvWriter.Flush();
                streamWriter.Flush();
               
                using (TransferUtility xferUtility = new TransferUtility(s3Client))
                {
                    // Make the transfer utility request to post the price data csv content
                    TransferUtilityUploadRequest request = new TransferUtilityUploadRequest()
                    {
                        BucketName = bucket,
                        Key = $"{response.ServiceCode}.csv",
                        InputStream = memoryStreamOut,
                        AutoResetStreamPosition = true,
                        AutoCloseStream = true
                    };

                    context.LogInfo($"Starting upload for:        {response.ServiceCode}");
                    context.LogInfo($"Output stream length:       {memoryStreamOut.Length}");

                    // Make the upload and record the task so we can wait for it finish
                    await xferUtility.UploadAsync(request);
                }

                context.LogInfo("Completed upload");
            }
            catch (Exception e)
            {
                context.LogError(e);
                string message = $"[ERROR] {DateTime.Now} {{{context.AwsRequestId}}} : There was a problem executing lambda for service {req.Service} - {e.Message}\n{e.StackTrace}";
                await SNSNotify(message, context);
                throw e;
            }
            finally
            {
                // Dispose all of the streams and writers used to
                // write the CSV content, we need to dispose of these here
                // so the memory stream doesn't get closed by disposing
                // of the writers too early, which will cause the transfer utility
                // to fail the upload
                foreach (IDisposable item in disposables)
                {
                    try
                    {
                        item.Dispose();
                    }
                    catch { }
                }

                // Make sure memory is cleaned up
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }

        #endregion

        #region Private Functions

        /// <summary>
        /// Fills the csvwriter with the data from the product infor
        /// </summary>
        /// <param name="productInfo">The product info data</param>
        /// <param name="writer">The csv writer to fill</param>
        /// <param name="format">The format of the product info data</param>
        private async Task FillOutputStreamWriter(string productInfo, CsvWriter writer, Format format)
        {
            switch (format)
            {
                default:
                case Format.CSV:
                    {
                        // Fills the memory output stream
                        await this.GetFromCsv(productInfo, writer);
                        break;
                    }
                case Format.JSON:
                    {
                        // Fills the memory output stream
                        await this.GetFromJson(productInfo, writer);
                        break;
                    }
            }
        }

        /// <summary>
        /// Converts the price list data from csv into our formatted csv
        /// </summary>
        /// <param name="csv"></param>
        /// <param name="writer"></param>
        private async Task GetFromCsv(string csv, CsvWriter writer)
        {
            // Find the the beginning of the header line
            // and remove the version data, etc from the csv
            int index = csv.IndexOf("\"SKU\",");

            using (MemoryStream mstream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(csv, index, csv.Length - index)))
            {
                using (StreamReader streamReader = new StreamReader(mstream))
                {
                    using (CsvReader reader = new CsvReader(streamReader))
                    {
                        // Make all of the headers lowercase so we don't have to worry about
                        // case sensitivity later
                        reader.Configuration.PrepareHeaderForMatch = Header => Header.ToLower();

                        reader.Read(); // Advance to the next row, which is the header row
                        reader.ReadHeader(); // Read the headers

                        List<CsvRowItem> rows = new List<CsvRowItem>();

                        while (reader.Read()) // Read all lines in the CSV
                        {
                            // Will return null if it's a record we're not concerned about
                            CsvRowItem row = CsvRowItem.Build(reader);

                            if (row != null)
                            {
                                rows.Add(row);
                            }
                        } // Close while loop

                        try
                        {
                            List<ReservedInstancePricingTerm> terms = new List<ReservedInstancePricingTerm>();

                            foreach (IGrouping<string, CsvRowItem> item in rows.GroupBy(x => x.Sku))
                            {
                                try
                                {
                                    // Force the list to be enumerated to throw exceptions here
                                    // and catch them, for example if we can't find an on demand
                                    // pricing term in this set
                                    List<ReservedInstancePricingTerm> row = ReservedInstancePricingTerm.BuildFromCsv(item).ToList();
                                    terms.AddRange(row);
                                }
                                catch (Exception e)
                                {
                                    _context.LogError(e);
                                    await SNSNotify(e, _context);
                                    // Don't throw, at least populate with data that has all
                                    // required info
                                }
                            }

                            writer.WriteRecords<ReservedInstancePricingTerm>(terms);
                        }
                        catch (Exception e)
                        {
                            _context.LogError(e);
                            await SNSNotify(e, _context);
                            throw e;
                        }
                        
                    } // Close CsvReader
                } // Close StreamReader
            } // Close Memory input stream
        }
      
        /// <summary>
        /// Converts the price list data from json into our formatted csv
        /// </summary>
        /// <param name="json"></param>
        /// <param name="writer"></param>
        private async Task GetFromJson(string json, CsvWriter writer)
        {
            ProductOffer offer = ProductOffer.FromJson(json);

            /*  
             *  Old implementation, don't need such a complex grouping, just iterate
             *  every sku in the reserved terms and only process the ones that have a matching
             *  product and on demand term
             * 
             *   Hashset<string> ApplicableProductSkus = new Hashset<string>(Offer.Terms[Term.RESERVED].Keys)
             * 
             *   foreach (IGrouping<string, PricingTerm> CommonSkus in Offer.Terms
             *          .SelectMany(x => x.Value) // Get all of the product item dictionaries from on demand and reserved
             *          .Where(x => ApplicableProductSkus.Contains(x.Key)) // Only get the pricing terms for products we care about
             *          .SelectMany(x => x.Value) // Get all of the pricing term key value pairs
             *          .Select(x => x.Value) // Get just the pricing terms
             *          .GroupBy(x => x.Sku)) // Put all of the same skus together
             *   {
             *       try
             *       {
             *           IEnumerable<ReservedInstancePricingTerm> Terms = ReservedInstancePricingTerm.Build2(CommonSkus, Offer.Products[CommonSkus.Key]);
             *           writer.WriteRecords<ReservedInstancePricingTerm>(Terms);
             *       }
             *       catch (Exception e)
             *       {
             *           this._Context.LogError(e);
             *       }
             *   }
             */

            foreach (string Sku in offer.Terms[Term.RESERVED].Keys)
            {
                if (!offer.Products.ContainsKey(Sku))
                {
                    _context.LogWarning($"There is no product that matches the Sku {Sku}.");
                    continue;
                }

                if (!offer.Terms[Term.ON_DEMAND].ContainsKey(Sku))
                {
                    _context.LogWarning($"There is no on-demand pricing term for sku {Sku}.");
                    continue;
                }

                try
                {
                    IEnumerable<ReservedInstancePricingTerm> terms = ReservedInstancePricingTerm.Build(
                        offer.Products[Sku], // The product
                        offer.Terms[Term.ON_DEMAND][Sku].FirstOrDefault().Value, // OnDemand PricingTerm
                        offer.Terms[Term.RESERVED][Sku].Select(x => x.Value) // IEnumerable<PricingTerm> Reserved Terms
                    );

                    writer.WriteRecords<ReservedInstancePricingTerm>(terms);
                }
                catch (Exception e)
                {
                    _context.LogError(e);
                    await SNSNotify(e, _context);
                    throw e;
                }
            }
        }

        /// <summary>
        /// If configured, sends an SNS notification to a topic
        /// </summary>
        /// <param name="message"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        private static async Task SNSNotify(string message, ILambdaContext context)
        {
            if (!String.IsNullOrEmpty(snsTopic))
            {
                try
                {
                    PublishResponse response = await snsClient.PublishAsync(snsTopic, message, subject);

                    if (response.HttpStatusCode != HttpStatusCode.OK)
                    {
                        context.LogError($"Failed to send SNS notification with status code {(int)response.HttpStatusCode}.");
                    }
                }
                catch (Exception e)
                {
                    context.LogError("Failed to send SNS notification.", e);
                    throw e;
                }
            }
        }

        private static async Task SNSNotify(Exception e, ILambdaContext context)
        {
            await SNSNotify($"EXCEPTION: {e.GetType().FullName}\nMESSAGE: {e.Message}\nSTACKTRACE: {e.StackTrace}", context);
        }

        private static async Task SNSNotify(Exception e, string message, ILambdaContext context)
        {
            await SNSNotify($"{message}\nEXCEPTION: {e.GetType().FullName}\nMESSAGE: {e.Message}\nSTACKTRACE: {e.StackTrace}", context);
        }

        #endregion
    }
}
