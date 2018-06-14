using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Amazon.Lambda.Core;
using BAMCIS.AWSLambda.Common.Events;
using System.Collections.Concurrent;
using BAMCIS.AWSLambda.Common;
using BAMCIS.AWSPriceListApi;
using Amazon.S3.Transfer;
using System.IO;
using Amazon.S3;
using CsvHelper;
using System.Text.RegularExpressions;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace BAMCIS.LambdaFunctions.PriceListApiFormatter
{
    public class Entrypoint
    {
        #region Private Fields

        private ILambdaContext _Context;
        private static readonly IEnumerable<string> _Services = new string[] { "AmazonEC2", "AmazonRDS", "AmazonElastiCache" };
        private PriceListClient _Client;
        private IAmazonS3 _S3Client;

        private static readonly Regex _AllUsageTypes = new Regex(@"(?:\bBoxUsage\b|HeavyUsage|DedicatedUsage|NodeUsage|Multi-AZUsage|InstanceUsage|HostBoxUsage)", RegexOptions.IgnoreCase);
        #endregion


        #region Constructors

        /// <summary>
        /// Default constructor that Lambda will invoke.
        /// </summary>
        public Entrypoint()
        {
            this._Client = new PriceListClient();
            this._S3Client = new AmazonS3Client();
        }

        #endregion

        #region Public Methods

        public async Task Exec(CloudWatchScheduledEvent ev, ILambdaContext context)
        {
            this._Context = context;

            ConcurrentBag<GetProductResponse> Bag = new ConcurrentBag<GetProductResponse>();

            List<Task<GetProductResponse>> Responses = new List<Task<GetProductResponse>>();

            using (TransferUtility XferUtil = new TransferUtility(this._S3Client))
            {
                foreach (string Service in _Services)
                {
                    GetProductRequest Request = new GetProductRequest(Service)
                    {
                        Format = Format.CSV
                    };

                    Responses.Add(this._Client.GetProductAsync(Request));
                }

                string Bucket = Environment.GetEnvironmentVariable("BUCKET");

                List<Task> Uploads = new List<Task>();

                List<IDisposable> Disposables = new List<IDisposable>();

                try
                {
                    foreach (Task<GetProductResponse> Task in Responses.Interleaved())
                    {
                        GetProductResponse Response = await Task;

                        int Index = Response.ProductInfo.IndexOf("\"SKU\",");

                        MemoryStream MStreamOut = new MemoryStream();
                        Disposables.Add(MStreamOut);

                        TextWriter SWriter = new StreamWriter(MStreamOut);
                        Disposables.Add(SWriter);

                        CsvWriter Writer = new CsvWriter(SWriter);
                        Disposables.Add(Writer);

                        Writer.WriteHeader<PriceData>();

                        using (MemoryStream MStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(Response.ProductInfo, Index, Response.ProductInfo.Length - Index)))
                        {
                            using (StreamReader SReader = new StreamReader(MStream))
                            {
                                using (CsvReader Reader = new CsvReader(SReader))
                                {
                                    Reader.Configuration.PrepareHeaderForMatch = Header => Header.ToLower();

                                    Reader.Read(); // Advance to the next row, which is the header row
                                    Reader.ReadHeader(); // Read the headers

                                    while (Reader.Read()) // Read all lines in the CSV
                                    {
                                        if (Reader.TryGetField<string>("instance type", out string InstanceType) 
                                            && !String.IsNullOrEmpty(InstanceType)
                                            && Reader.TryGetField<string>("usagetype", out string UsageType)
                                            && _AllUsageTypes.IsMatch(UsageType)
                                        )
                                        {
                                            // Do this before we start a new record and flush the previous
                                            // record and add a line ending
                                            Writer.NextRecord();

                                            string Platform = String.Empty;

                                            Reader.TryGetField<string>("sku", out string SKU);
                                            Reader.TryGetField<string>("termtype", out string TermType);
                                            Reader.TryGetField<double>("priceperunit", out double PricePerUnit);
                                            Reader.TryGetField<string>("leasecontractlength", out string LeaseContractLength);

                                            if (!Reader.TryGetField<string>("purchaseoption", out string PurchaseOption) ||
                                                String.IsNullOrEmpty(PurchaseOption))
                                            {
                                                PurchaseOption = "On Demand";
                                            }


                                            if (!Reader.TryGetField<string>("offeringclass", out string OfferingClass) ||
                                                String.IsNullOrEmpty(OfferingClass))
                                            {
                                                OfferingClass = "standard";
                                            }

                                            // Only EC2 has tenancy
                                            if (!Reader.TryGetField<string>("tenancy", out string Tenancy))
                                            {
                                                Tenancy = "Shared";
                                            }

                                            Reader.TryGetField<string>("license model", out string LicenseModel);
                                            
                                            Reader.TryGetField<string>("operation", out string Operation);

                                            if (Response.ServiceCode.Equals("AmazonRDS", StringComparison.OrdinalIgnoreCase))
                                            {
                                                Reader.TryGetField<string>("database engine", out string DatabaseEngine);
                                                Reader.TryGetField<string>("database edition", out string DatabaseEdition);

                                                Platform = $"RDS {DatabaseEngine}";

                                                /*
                                                 * Database editions for SQL Server may be: 
                                                 * Enterprise
                                                 * Web                                           
                                                 * Standard
                                                 * 
                                                 * Datbase editions for Oracle may be:
                                                 * Standard
                                                 * Standard One
                                                 * Standard Two
                                                 * Enterprise
                                                 */

                                                switch (DatabaseEngine.ToLower())
                                                {
                                                    case "sql server":
                                                        {
                                                            switch (DatabaseEdition.ToLower())
                                                            {
                                                                case "enterprise":
                                                                    {
                                                                        Platform += " EE";
                                                                        break;
                                                                    }
                                                                case "standard":
                                                                    {
                                                                        Platform += " SE";
                                                                        break;
                                                                    }
                                                                case "web":
                                                                    {
                                                                        Platform += " Web";
                                                                        break;
                                                                    }
                                                            }
                                                            break;
                                                        }
                                                    case "oracle":
                                                        {
                                                            switch (DatabaseEdition.ToLower())
                                                            {
                                                                case "enterprise":
                                                                    {
                                                                        Platform += " EE";
                                                                        break;
                                                                    }
                                                                case "standard":
                                                                    {
                                                                        Platform += " SE";
                                                                        break;
                                                                    }
                                                                case "standard one":
                                                                    {
                                                                        Platform += " SE1";
                                                                        break;
                                                                    }
                                                                case "standard two":
                                                                    {
                                                                        Platform += " SE2";
                                                                        break;
                                                                    }
                                                            }

                                                            break;
                                                        }
                                                }

                                                switch (LicenseModel.ToLower())
                                                {
                                                    case "bring your own license":
                                                        {
                                                            Platform += " BYOL";
                                                            break;
                                                        }

                                                    default:
                                                    case "license included":
                                                    case "no license required":
                                                        {
                                                            break;
                                                        }
                                                }

                                                if (Reader.TryGetField<string>("deployment option", out string DeploymentOption))
                                                {
                                                    Platform += (DeploymentOption.Equals("Multi-AZ", StringComparison.OrdinalIgnoreCase) ? " Multi-AZ" : "");
                                                }
                                            }

                                            if (Response.ServiceCode.Equals("AmazonEC2", StringComparison.OrdinalIgnoreCase))
                                            {
                                                Reader.TryGetField<string>("operating system", out string OperatingSystem);
                                                Reader.TryGetField<string>("pre installed s/w", out string PreInstalledSW);
                                                Platform = $"{OperatingSystem}";

                                                switch (LicenseModel.ToLower())
                                                {
                                                    case "bring your own license":
                                                        {
                                                            Platform += " BYOL";
                                                            break;
                                                        }

                                                    default:
                                                    case "license included":
                                                    case "no license required":
                                                        {
                                                            break;
                                                        }
                                                }

                                                //If preinstalled software is present, like SQL, add it to the platform name
                                                if (!String.IsNullOrEmpty(PreInstalledSW) && !PreInstalledSW.Equals("NA", StringComparison.OrdinalIgnoreCase))
                                                {
                                                    Platform += $" with {PreInstalledSW}";
                                                }
                                            }

                                            if (Response.ServiceCode.Equals("AmazonElastiCache", StringComparison.OrdinalIgnoreCase))
                                            {
                                                Reader.TryGetField<string>("cache engine", out string CacheEngine);
                                                Platform = $"ElastiCache {CacheEngine}";
                                            }

                                            int Lease = String.IsNullOrEmpty(LeaseContractLength) ? 0 : Int32.Parse(Regex.Match(LeaseContractLength, "^([0-9]+)").Groups[1].Value);

                                            PriceData Data = new PriceData()
                                            {
                                                SKU = SKU,
                                                TermType = TermType,
                                                InstanceType = InstanceType,
                                                LeaseContractLength = Lease,
                                                OfferingClass = OfferingClass,
                                                Operation = Operation,
                                                Platform = Platform,
                                                PricePerUnit = PricePerUnit,
                                                PurchaseOption = PurchaseOption,
                                                Service = Response.ServiceCode,
                                                Tenancy = Tenancy,
                                                UsageType = UsageType,
                                                Region = RegionMapper.GetRegionFromUsageType(UsageType)
                                            };

                                            Writer.WriteRecord<PriceData>(Data);
                                        } // Close if statement
                                    } // Close while loop

                                    // Flush the last record but we don't need to add a new line
                                    Writer.Flush();
                                } // Close CsvReader
                            } // Close StreamReader
                        } // Close Memory input stream

                        TransferUtilityUploadRequest Request = new TransferUtilityUploadRequest()
                        {
                            BucketName = Bucket,
                            Key = $"{Response.ServiceCode}/index.csv",
                            InputStream = MStreamOut,
                            AutoResetStreamPosition = true,
                            AutoCloseStream = true
                        };

                        this._Context.LogInfo($"Starting upload for {Response.ServiceCode}.");

                        Uploads.Add(XferUtil.UploadAsync(Request));
                    }

                    Task.WaitAll(Uploads.ToArray());

                    this._Context.LogInfo("Completed all uploads");
                }
                finally
                {
                    foreach (IDisposable Item in Disposables)
                    {
                        try
                        {
                            Item.Dispose();
                        }
                        catch { }
                    }
                }
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        #endregion
    }
}
