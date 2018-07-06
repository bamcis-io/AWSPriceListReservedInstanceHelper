using Amazon.Lambda;
using Amazon.Lambda.Core;
using Amazon.Lambda.Model;
using Amazon.S3;
using Amazon.S3.Transfer;
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
using System.Text.RegularExpressions;
using System.Threading.Tasks;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
//[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace BAMCIS.LambdaFunctions.AWSPriceListReservedInstanceHelper
{

    /// <summary>
    /// The class that Lambda will call to invoke the Exec method
    /// </summary>
    public class Entrypoint
    {
        #region Private Fields

        private ILambdaContext _Context;
        private static readonly IEnumerable<string> _Services = new string[] { "AmazonEC2", "AmazonRDS", "AmazonElastiCache" };
        private PriceListClient _PriceListClient;
        private IAmazonS3 _S3Client;
        private AmazonLambdaClient _LambdaClient;

        // Use the pipe so that commas in strings don't cause an issue
        private static readonly string _DefaultDelimiter = "|";

        private static readonly Regex _AllUsageTypes = new Regex(@"(?:\bBoxUsage\b|HeavyUsage|DedicatedUsage|NodeUsage|Multi-AZUsage|InstanceUsage|HostBoxUsage)", RegexOptions.IgnoreCase);

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
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Initiates a lambda function for each service that we want to get from the price list api
        /// </summary>
        /// <param name="ev"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public async Task Split(CloudWatchScheduledEvent ev, ILambdaContext context)
        {
            List<Task<InvokeResponse>> Responses = new List<Task<InvokeResponse>>();

            foreach (string Service in _Services)
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

            context.LogInfo("All kickoff requests completed.");
        }

        /// <summary>
        /// Executes the lambda function to get the price list data for the 
        /// set of services we can buy reserved instances for
        /// </summary>
        /// <param name="ev"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public async Task Exec(ServiceRequest req, ILambdaContext context)
        {
            this._Context = context;

            if (req == null || String.IsNullOrEmpty(req.Service))
            {
                this._Context.LogError("No service was provided in the service request.");
                return;
            }

            // Get the product price data for the service
            Console.WriteLine($"Getting product data for {req.Service}");

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

                GetProductRequest ProductRequest = new GetProductRequest(req.Service)
                {
                    Format = InputFormat.Equals("csv") ? Format.CSV : Format.JSON
                };

                // Retrieve the finished get product price data response
                GetProductResponse Response = await this._PriceListClient.GetProductAsync(ProductRequest);

                switch (InputFormat)
                {
                    default:
                    case "csv":
                        {
                            // Fills the memory output stream
                            this.GetFromCsv(Response.ProductInfo, Writer);
                            break;
                        }
                    case "json":
                        {
                            // Fills the memory output stream
                            this.GetFromJson(Response.ProductInfo, Writer);
                            break;
                        }
                }

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
            }

            // Make sure memory is cleaned up
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        #endregion

        #region Private Functions

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

            // This holds the disposable stream and writer objects
            // that need to be disposed at the end
            List<IDisposable> Disposables = new List<IDisposable>();

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

                        foreach (ReservedInstancePricingTerm Term in Rows.GroupBy(x => x.Sku).SelectMany(x => ReservedInstancePricingTerm.Build(x)))
                        {
                            writer.WriteRecord<ReservedInstancePricingTerm>(Term);
                            writer.NextRecord();
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

            // Get the products that are actual instances
            HashSet<string> ApplicableProductSkus = new HashSet<string>(
                Offer.Products.Where(x =>
                {
                    var Attributes = x.Value.Attributes.ToDictionary(y => y.Key, y => y.Value, StringComparer.OrdinalIgnoreCase);

                    return Attributes.ContainsKey("instancetype") &&
                        Attributes.ContainsKey("usagetype") &&
                        Attributes.ContainsKey("operation") &&
                        Attributes.ContainsKey("servicecode");
                })
                .Select(x => x.Key).Distinct()
            );

            foreach (IGrouping<string, PricingTerm> CommonSkus in Offer.Terms
                       .SelectMany(x => x.Value) // Get all of the product item dictionaries from on demand and reserved
                       .Where(x => ApplicableProductSkus.Contains(x.Key)) // Only get the pricing terms for products we care about
                       .SelectMany(x => x.Value) // Get all of the pricing term key value pairs
                       .Select(x => x.Value) // Get just the pricing terms
                       .GroupBy(x => x.Sku)) // Put all of the same skus together
            {
                try
                {
                    IEnumerable<ReservedInstancePricingTerm> Terms = ReservedInstancePricingTerm.Build(CommonSkus, Offer.Products[CommonSkus.Key]);
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
