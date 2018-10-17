using Amazon.Lambda;
using Amazon.Lambda.Core;
using Amazon.Lambda.Model;
using Amazon.S3;
using Amazon.S3.Transfer;
using Amazon.SimpleNotificationService;
using BAMCIS.AWSLambda.Common;
using BAMCIS.AWSLambda.Common.Events;
using BAMCIS.AWSPriceListApi;
using BAMCIS.AWSPriceListApi.Serde;
using BAMCIS.LambdaFunctions.AWSPriceListReservedInstanceHelper.Models;
using CsvHelper;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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

        private ILambdaContext _Context;
        private PriceListClient _PriceListClient;
        private IAmazonS3 _S3Client;
        private AmazonLambdaClient _LambdaClient;
        private IAmazonSimpleNotificationService _SNSClient;

        // Use the pipe so that commas in strings don't cause an issue
        private static readonly string _DefaultDelimiter = "|";

        /// <summary>
        /// All of the usage types that indicate usage that might have an RI associated with it
        /// 
        /// InstanceUsage/Multi-AZUsage - RDS
        /// BoxUsage/HeavyUsage/DedicatedUsage/HostBoxUsage - EC2
        /// NodeUsage - ElastiCache
        /// Node - Redshift
        /// WriteCapacityUnit/ReadCapacityUnit - DynamoDB
        /// </summary>
        private static readonly Regex _AllUsageTypes = new Regex(@"(?:\bBoxUsage\b|HeavyUsage|DedicatedUsage|NodeUsage|Multi-AZUsage|InstanceUsage|HostBoxUsage|\bNode\b|\bWriteCapacityUnit|\bReadCapacityUnit)", RegexOptions.IgnoreCase);


        #endregion

        #region Constructors

        /// <summary>
        /// Default constructor that Lambda will invoke.
        /// </summary>
        public Entrypoint()
        {
            this._PriceListClient = new PriceListClient();
            this._S3Client = new AmazonS3Client();
            this._LambdaClient = new AmazonLambdaClient();
            this._SNSClient = new AmazonSimpleNotificationServiceClient();
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
            List<Task<InvokeResponse>> Responses = new List<Task<InvokeResponse>>();

            foreach (string Service in Constants.ReservableServices)
            {
                try
                {
                    InvokeRequest Req = new InvokeRequest()
                    {
                        FunctionName = System.Environment.GetEnvironmentVariable("FunctionName"),
                        Payload = $"{{\"service\":\"{Service}\"}}",
                        InvocationType = InvocationType.Event,
                        ClientContext = JsonConvert.SerializeObject(context.ClientContext, Formatting.None),
                    };

                    InvokeResponse Res = await this._LambdaClient.InvokeAsync(Req);
                    context.LogInfo($"Completed kickoff for {Service} with http status {(int)Res.StatusCode}.");
                }
                catch (Exception e)
                {
                    this._Context.LogError(e);
                    string SnsTopic = System.Environment.GetEnvironmentVariable("SNS");
                    if (!String.IsNullOrEmpty(SnsTopic))
                    {
                        try
                        {
                            string Message = $"[ERROR] {DateTime.Now} {{{this._Context.AwsRequestId}}} : There was a problem creating a lambda invocation request for service {Service} - {e.Message}";
                            await this._SNSClient.PublishAsync(SnsTopic, Message, "[ERROR] AWS Price List Reserved Instance Helper");
                        }
                        catch (Exception e2)
                        {
                            this._Context.LogError("Failed to send SNS message on exception", e2);
                        }
                    }
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
            this._Context = context;

            if (req == null || String.IsNullOrEmpty(req.Service))
            {
                this._Context.LogError("No service was provided in the service request.");
                return;
            }

            // Get the product price data for the service
            this._Context.LogInfo($"Getting product data for {req.Service}");

            string Bucket = System.Environment.GetEnvironmentVariable("BUCKET");
            string Delimiter = System.Environment.GetEnvironmentVariable("DELIMITER");
            string InputFormat = System.Environment.GetEnvironmentVariable("PRICELIST_FORMAT");

            if (String.IsNullOrEmpty(InputFormat))
            {
                InputFormat = "csv";
            }

            InputFormat = InputFormat.ToLower().Trim();

            this._Context.LogInfo($"Using price list format: {InputFormat}");

            if (String.IsNullOrEmpty(Delimiter))
            {
                Delimiter = _DefaultDelimiter;
            }

            // This holds the disposable stream and writer objects
            // that need to be disposed at the end
            List<IDisposable> Disposables = new List<IDisposable>();

            try
            {
                // Will hold the stream of price data content that the 
                // transfer utility will send
                MemoryStream MStreamOut = new MemoryStream();
                Disposables.Add(MStreamOut);

                // Provided to the csv writer to write to the memory stream
                TextWriter SWriter = new StreamWriter(MStreamOut);
                Disposables.Add(SWriter);

                // The csv writer to write the price data objects
                CsvWriter Writer = new CsvWriter(SWriter);
                Writer.Configuration.Delimiter = Delimiter;
                Disposables.Add(Writer);

                // Write the header to the csv
                Writer.WriteHeader<ReservedInstancePricingTerm>();
                Writer.NextRecord();

                // Create the product request with the right format
                GetProductRequest ProductRequest = new GetProductRequest(req.Service)
                {
                    Format = InputFormat.Equals("json", StringComparison.OrdinalIgnoreCase) ? Format.JSON : Format.CSV
                };

                this._Context.LogInfo("Getting price list offer file.");

                // Retrieve the finished get product price data response
                GetProductResponse Response = await this._PriceListClient.GetProductAsync(ProductRequest);

                this._Context.LogInfo("Parsing price list data.");

                // Fill the output stream
                this.FillOutputStreamWriter(Response.ProductInfo, Writer, ProductRequest.Format);

                // Make sure everything is written out since we don't dispose
                // of these till later, if the textwriter isn't flushed
                // you will lose content from the csv file
                SWriter.Flush();
                Writer.Flush();

                using (TransferUtility XferUtil = new TransferUtility(this._S3Client))
                {
                    // Make the transfer utility request to post the price data csv content
                    TransferUtilityUploadRequest Request = new TransferUtilityUploadRequest()
                    {
                        BucketName = Bucket,
                        Key = $"{Response.ServiceCode}.csv",
                        InputStream = MStreamOut,
                        AutoResetStreamPosition = true,
                        AutoCloseStream = true
                    };

                    this._Context.LogInfo($"Starting upload for:        {Response.ServiceCode}");
                    this._Context.LogInfo($"Output stream length:       {MStreamOut.Length}");

                    // Make the upload and record the task so we can wait for it finish
                    await XferUtil.UploadAsync(Request);
                }

                this._Context.LogInfo("Completed upload");
            }
            catch (Exception e)
            {
                this._Context.LogError(e);

                string SnsTopic = System.Environment.GetEnvironmentVariable("SNS");
                if (!String.IsNullOrEmpty(SnsTopic))
                {
                    try
                    {
                        string Message = $"[ERROR] {DateTime.Now} {{{this._Context.AwsRequestId}}} : There was a problem executing lambda for service {req.Service} - {e.Message}";
                        await this._SNSClient.PublishAsync(SnsTopic, Message, "[ERROR] AWS Price List Reserved Instance Helper");
                    }
                    catch (Exception e2)
                    {
                        this._Context.LogError("Failed to send SNS message on exception", e2);
                    }
                }
            }
            finally
            {
                // Dispose all of the streams and writers used to
                // write the CSV content, we need to dispose of these here
                // so the memory stream doesn't get closed by disposing
                // of the writers too early, which will cause the transfer utility
                // to fail the upload
                foreach (IDisposable Item in Disposables)
                {
                    try
                    {
                        Item.Dispose();
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
        private void FillOutputStreamWriter(string productInfo, CsvWriter writer, Format format)
        {
            switch (format)
            {
                default:
                case Format.CSV:
                    {
                        // Fills the memory output stream
                        this.GetFromCsv(productInfo, writer);
                        break;
                    }
                case Format.JSON:
                    {
                        // Fills the memory output stream
                        this.GetFromJson(productInfo, writer);
                        break;
                    }
            }
        }

        /// <summary>
        /// Converts the price list data from csv into our formatted csv
        /// </summary>
        /// <param name="csv"></param>
        /// <param name="writer"></param>
        private void GetFromCsv(string csv, CsvWriter writer)
        {
            // Find the the beginning of the header line
            // and remove the version data, etc from the csv
            int Index = csv.IndexOf("\"SKU\",");

            using (MemoryStream MStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(csv, Index, csv.Length - Index)))
            {
                using (StreamReader SReader = new StreamReader(MStream))
                {
                    using (CsvReader Reader = new CsvReader(SReader))
                    {
                        // Make all of the headers lowercase so we don't have to worry about
                        // case sensitivity later
                        Reader.Configuration.PrepareHeaderForMatch = Header => Header.ToLower();

                        Reader.Read(); // Advance to the next row, which is the header row
                        Reader.ReadHeader(); // Read the headers

                        List<CsvRowItem> Rows = new List<CsvRowItem>();

                        while (Reader.Read()) // Read all lines in the CSV
                        {
                            // Will return null if it's a record we're not concerned about
                            CsvRowItem Row = CsvRowItem.Build(Reader);

                            if (Row != null)
                            {
                                Rows.Add(Row);
                            }
                        } // Close while loop

                        try
                        {
                            writer.WriteRecords<ReservedInstancePricingTerm>(
                                Rows.GroupBy(x => x.Sku).SelectMany(x => ReservedInstancePricingTerm.Build(x))
                            );
                        }
                        catch (Exception e)
                        {
                            this._Context.LogError(e);
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
        private void GetFromJson(string json, CsvWriter writer)
        {
            ProductOffer Offer = ProductOffer.FromJson(json);

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

            foreach (string Sku in Offer.Terms[Term.RESERVED].Keys)
            {
                if (!Offer.Products.ContainsKey(Sku))
                {
                    this._Context.LogError($"There is no product that matches the Sku {Sku}.");
                    continue;
                }

                if (!Offer.Terms[Term.ON_DEMAND].ContainsKey(Sku))
                {
                    this._Context.LogError($"There is no on-demand pricing term for sku {Sku}.");
                    continue;
                }

                try
                {
                    IEnumerable<ReservedInstancePricingTerm> Terms = ReservedInstancePricingTerm.Build(
                        Offer.Products[Sku], // The product
                        Offer.Terms[Term.ON_DEMAND][Sku].FirstOrDefault().Value, // OnDemand PricingTerm
                        Offer.Terms[Term.RESERVED][Sku].Select(x => x.Value) // IEnumerable<PricingTerm> Reserved Terms
                    );

                    writer.WriteRecords<ReservedInstancePricingTerm>(Terms);
                }
                catch (Exception e)
                {
                    this._Context.LogError(e);
                }
            }
        }

        #endregion
    }
}
