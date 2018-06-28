using Amazon.Lambda;
using Amazon.Lambda.Core;
using Amazon.Lambda.Model;
using Amazon.S3;
using Amazon.S3.Transfer;
using BAMCIS.AWSLambda.Common;
using BAMCIS.AWSLambda.Common.Events;
using BAMCIS.AWSPriceListApi;
using BAMCIS.AWSPriceListApi.Serde;
using BAMCIS.LambdaFunctions.PriceListApiFormatter.Models;
using CsvHelper;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace BAMCIS.LambdaFunctions.PriceListApiFormatter
{
    /// <summary>
    /// The class that Lambda will call to invoke the Exec method
    /// </summary>
    public class EntrypointJson
    {
        #region Private Fields

        private ILambdaContext _Context;
        private static readonly IEnumerable<string> _Services = new string[] { "AmazonEC2", "AmazonRDS", "AmazonElastiCache" };
        private PriceListClient _PriceListClient;
        private IAmazonS3 _S3Client;
        private AmazonLambdaClient _LambdaClient;

        private static readonly Regex _AllUsageTypes = new Regex(@"(?:\bBoxUsage\b|HeavyUsage|DedicatedUsage|NodeUsage|Multi-AZUsage|InstanceUsage|HostBoxUsage)", RegexOptions.IgnoreCase);

        #endregion

        #region Constructors

        /// <summary>
        /// Default constructor that Lambda will invoke.
        /// </summary>
        public EntrypointJson()
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

                Responses.Add(this._LambdaClient.InvokeAsync(Req));
            }

            foreach (Task<InvokeResponse> Response in Responses.Interleaved())
            {
                InvokeResponse Res = await Response;
                using (StreamReader Reader = new StreamReader(Res.Payload))
                {
                    context.LogInfo($"Completed kickoff for {Reader.ReadToEnd()} with http status {(int)Res.StatusCode}.");
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
        public async Task Exec(ServiceRequest req, ILambdaContext context)
        {
            this._Context = context;

            string Bucket = System.Environment.GetEnvironmentVariable("BUCKET");

            // This holds the disposable stream and writer objects
            // that need to be disposed at the end
            List<IDisposable> Disposables = new List<IDisposable>();

            // Get the product price data for each service

            Console.WriteLine($"Getting product data for {req.Service}");
            GetProductRequest ProductRequest = new GetProductRequest(req.Service)
            {
                Format = Format.JSON
            };

            // Retrieve the finished get product price data response
            GetProductResponse Response = await this._PriceListClient.GetProductAsync(ProductRequest);

            try
            {
                using (TransferUtility XferUtil = new TransferUtility(this._S3Client))
                {
                    ProductOffer Offer = ProductOffer.FromJson(Response.ProductInfo);

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

                    this._Context.LogInfo($"Found {ApplicableProductSkus.Count} applicable products for {Response.ServiceCode}.");

                    // Serialize the data into CSV and write it to an output stream
                    MemoryStream MStreamOut = new MemoryStream();
                    Disposables.Add(MStreamOut);

                    StreamWriter SWriter = new StreamWriter(MStreamOut);
                    Disposables.Add(SWriter);

                    CsvWriter Writer = new CsvWriter(SWriter);

                    // Write all of the CSV data to the stream
                    Writer.WriteHeader<ReservedInstancePricingTerm>();
                    Writer.NextRecord();

                    // This leaves us with 1 row per unique configuration, the row contains
                    // pricing information for a specific purchase option, like 1 year all upfront standard
                    // or 3 year partial upfront convertible, or on demand
                    //List<ReservedInstancePricingTerm> Terms = Offer.Terms
                    //   .SelectMany(x => x.Value) // Get all of the product item dictionaries from on demand and reserved
                    //   .Where(x => ApplicableProductSkus.Contains(x.Key)) // Only get the pricing terms for products we care about
                    //   .SelectMany(x => x.Value) // Get all of the pricing term key value pairs
                    //   .Select(x => x.Value) // Get just the pricing terms
                    //   .GroupBy(x => x.Sku) // Put all of the same skus together
                    //   .SelectMany(x => TryGet(x, Offer.Products[x.Key])).ToList(); // Take all the pricing terms and convert them

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
                            Writer.WriteRecords<ReservedInstancePricingTerm>(Terms);
                        }
                        catch (Exception e)
                        {
                            this._Context.LogError(e);
                        }
                    }

                    this._Context.LogInfo($"Completed generating reserved instance pricing terms for {Response.ServiceCode}");

                    // Flush the Writers here since they don't get disposed of until later
                    Writer.Flush();
                    SWriter.Flush();

                    // Make the transfer utility request to post the price data csv content
                    TransferUtilityUploadRequest Request = new TransferUtilityUploadRequest()
                    {
                        BucketName = Bucket,
                        Key = $"{Response.ServiceCode}.csv",
                        InputStream = MStreamOut,
                        AutoResetStreamPosition = true,
                        AutoCloseStream = true
                    };

                    this._Context.LogInfo($"Starting upload for:  {Response.ServiceCode}.");
                    this._Context.LogInfo($"Output stream length: {MStreamOut.Length}");

                    // Make the upload and record the task so we can wait for it finish
                    await XferUtil.UploadAsync(Request);

                    this._Context.LogInfo("Completed upload");
                } // Close using statement for TransferUtility
            } // Close try block
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
            } // Close finally block

            // Make sure memory is cleaned up
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        #endregion
    }
}
