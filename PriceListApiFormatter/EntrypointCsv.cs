//using Amazon.Lambda.Core;
//using Amazon.S3;
//using Amazon.S3.Transfer;
//using BAMCIS.AWSLambda.Common;
//using BAMCIS.AWSLambda.Common.Events;
//using BAMCIS.AWSPriceListApi;
//using CsvHelper;
//using System;
//using System.Collections.Generic;
//using System.IO;
//using System.Text;
//using System.Text.RegularExpressions;
//using System.Threading.Tasks;

//// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
////[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

//namespace BAMCIS.LambdaFunctions.PriceListApiFormatter
//{

//    /// <summary>
//    /// The class that Lambda will call to invoke the Exec method
//    /// </summary>
//    public class EntrypointCsv
//    {
//        #region Private Fields

//        private ILambdaContext _Context;
//        private static readonly IEnumerable<string> _Services = new string[] { "AmazonEC2", "AmazonRDS", "AmazonElastiCache" };
//        private PriceListClient _PriceListClient;
//        private IAmazonS3 _S3Client;

//        private static readonly Regex _AllUsageTypes = new Regex(@"(?:\bBoxUsage\b|HeavyUsage|DedicatedUsage|NodeUsage|Multi-AZUsage|InstanceUsage|HostBoxUsage)", RegexOptions.IgnoreCase);

//        #endregion

//        #region Constructors

//        /// <summary>
//        /// Default constructor that Lambda will invoke.
//        /// </summary>
//        public EntrypointCsv()
//        {
//            this._PriceListClient = new PriceListClient();
//            this._S3Client = new AmazonS3Client();
//        }

//        #endregion

//        #region Public Methods

//        /// <summary>
//        /// Executes the lambda function to get the price list data for the 
//        /// set of services we can buy reserved instances for
//        /// </summary>
//        /// <param name="ev"></param>
//        /// <param name="context"></param>
//        /// <returns></returns>
//        public async Task Exec(CloudWatchScheduledEvent ev, ILambdaContext context)
//        {
//            this._Context = context;

//            List<Task<GetProductResponse>> Responses = new List<Task<GetProductResponse>>();

//            // Get the product price data for each service
//            foreach (string Service in _Services)
//            {
//                GetProductRequest Request = new GetProductRequest(Service)
//                {
//                    Format = Format.CSV
//                };

//                Responses.Add(this._PriceListClient.GetProductAsync(Request));
//            }

//            string Bucket = Environment.GetEnvironmentVariable("BUCKET");

//            // This holds the TransferUtility upload tasks
//            List<Task> Uploads = new List<Task>();

//            // This holds the disposable stream and writer objects
//            // that need to be disposed at the end
//            List<IDisposable> Disposables = new List<IDisposable>();

//            try
//            {
//                using (TransferUtility XferUtil = new TransferUtility(this._S3Client))
//                {
//                    foreach (Task<GetProductResponse> Task in Responses.Interleaved())
//                    {
//                        // Retrieve the finished get product price data response
//                        GetProductResponse Response = await Task;

//                        // Find the the beginning of the header line
//                        // and remove the version data, etc from the csv
//                        int Index = Response.ProductInfo.IndexOf("\"SKU\",");

//                        // Will hold the stream of price data content that the 
//                        // transfer utility will send
//                        MemoryStream MStreamOut = new MemoryStream();
//                        Disposables.Add(MStreamOut);

//                        // Provided to the csv writer to write to the memory stream
//                        TextWriter SWriter = new StreamWriter(MStreamOut);
//                        Disposables.Add(SWriter);

//                        // The csv writer to write the price data objects
//                        CsvWriter Writer = new CsvWriter(SWriter);
//                        Disposables.Add(Writer);

//                        // Set the header in the output
//                        Writer.WriteHeader<PriceData>();

//                        using (MemoryStream MStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(Response.ProductInfo, Index, Response.ProductInfo.Length - Index)))
//                        {
//                            using (StreamReader SReader = new StreamReader(MStream))
//                            {
//                                using (CsvReader Reader = new CsvReader(SReader))
//                                {
//                                    // Make all of the headers lowercase so we don't have to worry about
//                                    // case sensitivity later
//                                    Reader.Configuration.PrepareHeaderForMatch = Header => Header.ToLower();

//                                    Reader.Read(); // Advance to the next row, which is the header row
//                                    Reader.ReadHeader(); // Read the headers

//                                    while (Reader.Read()) // Read all lines in the CSV
//                                    {
//                                        // Will return null if it's a record we're not concerned about
//                                        PriceData Data = ParseRecord(Reader);

//                                        if (Data != null)
//                                        {
//                                            // Do this before we start a new record and flush the previous
//                                            // record and add a line ending
//                                            Writer.NextRecord();
//                                            Writer.WriteRecord<PriceData>(Data);
//                                        }
//                                    } // Close while loop

//                                    // Flush the last record 
//                                    Writer.NextRecord();
//                                } // Close CsvReader
//                            } // Close StreamReader
//                        } // Close Memory input stream

//                        // Make sure everything is written out since we don't dispose
//                        // of these till later, if the textwriter isn't flushed
//                        // you will lose content from the csv file
//                        SWriter.Flush();
//                        Writer.Flush();
                      
//                        // Make the transfer utility request to post the price data csv content
//                        TransferUtilityUploadRequest Request = new TransferUtilityUploadRequest()
//                        {
//                            BucketName = Bucket,
//                            Key = $"{Response.ServiceCode}/index.csv",
//                            InputStream = MStreamOut,
//                            AutoResetStreamPosition = true,
//                            AutoCloseStream = true
//                        };

//                        this._Context.LogInfo($"Starting upload for:  {Response.ServiceCode}.");
//                        this._Context.LogInfo($"Output stream length: {MStreamOut.Length}");

//                        // Make the upload and record the task so we can wait for it finish
//                        Uploads.Add(XferUtil.UploadAsync(Request));
//                    } // Close foreach task from getting the price list files

//                    // Wait for all of the uploads to finish
//                    Task.WaitAll(Uploads.ToArray());

//                    this._Context.LogInfo("Completed all uploads");
//                } // Close using statement for TransferUtility
//            } // Close try block
//            finally
//            {
//                // Dispose all of the streams and writers used to
//                // write the CSV content, we need to dispose of these here
//                // so the memory stream doesn't get closed by disposing
//                // of the writers too early, which will cause the transfer utility
//                // to fail the upload
//                foreach (IDisposable Item in Disposables)
//                {
//                    try
//                    {
//                        Item.Dispose();
//                    }
//                    catch { }
//                }
//            } // Close finally block

//            // Make sure memory is cleaned up
//            GC.Collect();
//            GC.WaitForPendingFinalizers();
//        }

//        #endregion

//        #region Private Methods

//        /// <summary>
//        /// Parses the current record from the csv reader
//        /// </summary>
//        /// <param name="reader"></param>
//        /// <returns>The price data or null if the record doesn't match specific criteria</returns>
//        private static dynamic ParseRecord(CsvReader reader)
//        {
//            if (reader.TryGetField<string>("instance type", out string InstanceType)
//                && !String.IsNullOrEmpty(InstanceType)
//                && reader.TryGetField<string>("usagetype", out string UsageType)
//                && _AllUsageTypes.IsMatch(UsageType)
//                && reader.TryGetField<string>("servicecode", out string ServiceCode)
//                && !String.IsNullOrEmpty(ServiceCode)
//            )
//            {
//                reader.TryGetField<string>("sku", out string Sku);
//                reader.TryGetField<string>("termtype", out string TermType);
//                reader.TryGetField<double>("priceperunit", out double PricePerUnit);
//                reader.TryGetField<string>("leasecontractlength", out string LeaseContractLength);
//                reader.TryGetField<string>("pricedescription", out string PriceDescription);
//                reader.TryGetField<string>("offertermcode", out string OfferTermCode);

//                if (!reader.TryGetField<string>("purchaseoption", out string PurchaseOption) ||
//                    String.IsNullOrEmpty(PurchaseOption))
//                {
//                    PurchaseOption = "On Demand";
//                }

//                if (!reader.TryGetField<string>("offeringclass", out string OfferingClass) ||
//                    String.IsNullOrEmpty(OfferingClass))
//                {
//                    OfferingClass = "standard";
//                }

//                // Only EC2 has tenancy
//                if (!reader.TryGetField<string>("tenancy", out string Tenancy))
//                {
//                    Tenancy = "Shared";
//                }

//                reader.TryGetField<string>("operation", out string Operation);

//                int Lease = String.IsNullOrEmpty(LeaseContractLength) ? 0 : Int32.Parse(Regex.Match(LeaseContractLength, "^([0-9]+)").Groups[1].Value);

//                // See if this is a reserved term
//                if (Lease > 0 &&
//                    !PurchaseOption.Equals("On Demand", StringComparison.OrdinalIgnoreCase))
//                {
//                    // HERE IS THE BIG PROBLEM AND WHY WE NEED TO USE JSON
//                    // THE PRICE DIMENSIONS AREN'T GROUPED IN ANY WAY, SO THIS MAY BE 
//                    // AN UPFRONT COST OR IT MAY BE A RECURRING COST, WHICH MEANS WE'D 
//                    // HAVE TO PARSE EACH ROW, HOLD IT IN MEMORY, THEN GROUP ROWS BASED ON SKU, 
//                    // PURCHASE OPTION, LEASE, OFFERING, ETC, AND THEN COMBINE THEM, WHICH
//                    // I THINK WOULD BE LESS EFFICIENT THAN JUST DESERIALIZING THE JSON
//                    /*
//                    string Upfront = term.PriceDimensions.Values.Select(x => x.Description).FirstOrDefault(x => x.ToLower().Equals("upfront fee"));

//                    // It may not have an upfront fee if it's a no upfront RI
//                    if (!String.IsNullOrEmpty(Upfront))
//                    {
//                        UpfrontFee = Double.Parse(Upfront);
//                    }
//                    */
//                }

//                return new PriceData()
//                {
//                    SKU = Sku,
//                    TermType = TermType,
//                    InstanceType = InstanceType,
//                    LeaseContractLength = Lease,
//                    OfferingClass = OfferingClass,
//                    Operation = Operation,
//                    Platform = GetPlatform(reader),
//                    PricePerUnit = PricePerUnit,
//                    PurchaseOption = PurchaseOption,
//                    Service = ServiceCode,
//                    Tenancy = Tenancy,
//                    UsageType = UsageType,
//                    Region = RegionMapper.GetRegionFromUsageType(UsageType),
//                    PriceDescription = PriceDescription
//                };
//            }
//            else
//            {
//                return null;
//            }
//        }

//        /// <summary>
//        /// Gets the platform from the csv row data
//        /// </summary>
//        /// <param name="reader"></param>
//        /// <returns></returns>
//        private static string GetPlatform(CsvReader reader)
//        {
//            if (reader.TryGetField<string>("servicecode", out string ServiceCode))
//            {
//                StringBuilder Buffer = new StringBuilder();

//                reader.TryGetField<string>("license model", out string LicenseModel);

//                switch (ServiceCode.ToLower())
//                {
//                    case "amazonrds":
//                        {
//                            reader.TryGetField<string>("database engine", out string DatabaseEngine);
//                            reader.TryGetField<string>("database edition", out string DatabaseEdition);

//                            Buffer.Append("RDS ").Append(DatabaseEngine);

//                            /*
//                             * Database editions for SQL Server may be: 
//                             * Enterprise
//                             * Web                                           
//                             * Standard
//                             * 
//                             * Datbase editions for Oracle may be:
//                             * Standard
//                             * Standard One
//                             * Standard Two
//                             * Enterprise
//                             */

//                            switch (DatabaseEngine.ToLower())
//                            {
//                                case "sql server":
//                                    {
//                                        switch (DatabaseEdition.ToLower())
//                                        {
//                                            case "enterprise":
//                                                {
//                                                    Buffer.Append(" EE");
//                                                    break;
//                                                }
//                                            case "standard":
//                                                {
//                                                    Buffer.Append(" SE");
//                                                    break;
//                                                }
//                                            case "web":
//                                                {
//                                                    Buffer.Append(" Web");
//                                                    break;
//                                                }
//                                        }
//                                        break;
//                                    }
//                                case "oracle":
//                                    {
//                                        switch (DatabaseEdition.ToLower())
//                                        {
//                                            case "enterprise":
//                                                {
//                                                    Buffer.Append(" EE");
//                                                    break;
//                                                }
//                                            case "standard":
//                                                {
//                                                    Buffer.Append(" SE");
//                                                    break;
//                                                }
//                                            case "standard one":
//                                                {
//                                                    Buffer.Append(" SE1");
//                                                    break;
//                                                }
//                                            case "standard two":
//                                                {
//                                                    Buffer.Append(" SE2");
//                                                    break;
//                                                }
//                                        }

//                                        break;
//                                    }
//                            }

//                            switch (LicenseModel.ToLower())
//                            {
//                                case "bring your own license":
//                                    {
//                                        Buffer.Append(" BYOL");
//                                        break;
//                                    }

//                                default:
//                                case "license included":
//                                case "no license required":
//                                    {
//                                        break;
//                                    }
//                            }

//                            if (reader.TryGetField<string>("deployment option", out string DeploymentOption) &&
//                                DeploymentOption.Equals("Multi-AZ", StringComparison.OrdinalIgnoreCase))
//                            {
//                                Buffer.Append(" Multi-AZ");
//                            }

//                            break;
//                        }
//                    case "amazonec2":
//                        {

//                            reader.TryGetField<string>("operating system", out string OperatingSystem);
//                            reader.TryGetField<string>("pre installed s/w", out string PreInstalledSW);
//                            Buffer.Append(OperatingSystem);

//                            switch (LicenseModel.ToLower())
//                            {
//                                case "bring your own license":
//                                    {
//                                        Buffer.Append(" BYOL");
//                                        break;
//                                    }

//                                default:
//                                case "license included":
//                                case "no license required":
//                                    {
//                                        break;
//                                    }
//                            }

//                            //If preinstalled software is present, like SQL, add it to the platform name
//                            if (!String.IsNullOrEmpty(PreInstalledSW) && !PreInstalledSW.Equals("NA", StringComparison.OrdinalIgnoreCase))
//                            {
//                                Buffer.Append(" with ").Append(PreInstalledSW);
//                            }

//                            break;
//                        }
//                    case "amazonelasticache":
//                        {
//                            reader.TryGetField<string>("cache engine", out string CacheEngine);
//                            Buffer.Append("ElastiCache ").Append(CacheEngine);

//                            break;
//                        }
//                }

//                return Buffer.ToString();
//            }
//            else
//            {
//                return "UNKNOWN";
//            }
//        }

//        #endregion
//    }
//}
