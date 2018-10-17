using BAMCIS.AWSPriceListApi.Serde;
using CsvHelper;
using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace BAMCIS.LambdaFunctions.AWSPriceListReservedInstanceHelper.Models
{
    /// <summary>
    /// Represents a row of data from the price list API when presented in csv format
    /// </summary>
    public class CsvRowItem
    {
        #region Private Fields

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

        /// <summary>
        /// Finds the amount of memory allocated to an instance
        /// This should be used only after removing commas from the input string
        /// </summary>
        private static readonly Regex _MemoryRegex = new Regex(@"^\s*([0-9]+(?:\.?[0-9]+)?)\s+GiB\s*$", RegexOptions.IgnoreCase);

        #endregion

        #region Public Properties

        /// <summary>
        /// The produce sku
        /// </summary>
        public string Sku { get; }

        /// <summary>
        /// The specific offer term code for the offering
        /// </summary>
        public string OfferTermCode { get; }

        /// <summary>
        /// The type of the term, either OnDemand or Reserved
        /// </summary>
        public Term TermType { get; }

        /// <summary>
        /// The length of the contract, typically 1 or 3 years
        /// </summary>
        public int LeaseContractLength { get; }

        /// <summary>
        /// The price per unit of the line item, which might be an hourly recurring free or an upfront fee
        /// </summary>
        public double PricePerUnit { get; }

        /// <summary>
        /// The number of virtual CPUs assigned to the instance
        /// </summary>
        public int vCPU { get; }

        /// <summary>
        /// The amount of memory in GiB assigned to the instance
        /// </summary>
        public double Memory { get; }

        /// <summary>
        /// The purchase option of the instance, like No Upfront, or All Upfront, or On Demand
        /// </summary>
        public PurchaseOption PurchaseOption { get; }

        /// <summary>
        /// The offering class, either Standard or Convertible
        /// </summary>
        public OfferingClass OfferingClass { get; }

        /// <summary>
        /// The tenancy of the instance, either Shared or Dedicated
        /// </summary>
        public string Tenancy { get; }

        /// <summary>
        /// The instance type
        /// </summary>
        public string InstanceType { get; }

        /// <summary>
        /// The platform of the instance
        /// </summary>
        public string Platform { get; }

        /// <summary>
        /// The operating system or service, like Windows or Linux or RHEL or ElastiCache Redis
        /// </summary>
        public string OperatingSystem { get; }

        /// <summary>
        /// The operation code
        /// </summary>
        public string Operation { get; }

        /// <summary>
        /// The usage type code
        /// </summary>
        public string UsageType { get; }

        /// <summary>
        /// The service code, like AmazonEC2
        /// </summary>
        public string ServiceCode { get; }

        /// <summary>
        /// The region the price data is applicable to
        /// </summary>
        public string Region { get; }

        /// <summary>
        /// The description of the charge, like Upfront Fee or $0.05 per instance hour
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// A key comprised of {lease term}::{purchase option}::{offering class}
        /// </summary>
        public string Key { get; }

        #endregion

        #region Constructors

        public CsvRowItem(
            string sku,
            string offerTermCode,
            Term termType,
            int leaseContractLength,
            double pricePerUnit,
            int vcpu,
            double memory,
            PurchaseOption purchaseOption,
            OfferingClass offeringClass,
            string tenancy,
            string instanceType,
            string platform,
            string operatingSystem,
            string operation,
            string usageType,
            string serviceCode,
            string region,
            string description
        )
        {
            this.Sku = sku;
            this.OfferTermCode = offerTermCode;
            this.TermType = termType;
            this.LeaseContractLength = leaseContractLength;
            this.PricePerUnit = pricePerUnit;
            this.vCPU = vcpu;
            this.Memory = memory;
            this.PurchaseOption = purchaseOption;
            this.OfferingClass = offeringClass;
            this.Tenancy = tenancy;
            this.InstanceType = instanceType;
            this.Platform = platform;
            this.OperatingSystem = operatingSystem;
            this.Operation = operation;
            this.UsageType = usageType;
            this.ServiceCode = serviceCode;
            this.Region = region;
            this.Description = description;

            this.Key = $"{this.LeaseContractLength}::{this.PurchaseOption}::{this.OfferingClass}";
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Builds a row item from the current line of the csv reader, this method
        /// does not change the position of the csv reader
        /// </summary>
        /// <param name="reader">The csv reader to read from</param>
        /// <returns></returns>
        public static CsvRowItem Build(CsvReader reader)
        {
            string InstanceType = String.Empty;

            // The field names are case sensitive
            if (reader.TryGetField<string>("operation", out string Operation)
               && !String.IsNullOrEmpty(Operation)
               && reader.TryGetField<string>("usagetype", out string UsageType)
               && _AllUsageTypes.IsMatch(UsageType)
               && reader.TryGetField<string>("servicecode", out string ServiceCode)
               && !String.IsNullOrEmpty(ServiceCode)
               && (Constants.InstanceBasedReservableServices.Contains(ServiceCode) ? reader.TryGetField("instance type", out InstanceType) : true)
           )
            {
                reader.TryGetField<string>("sku", out string Sku);
                reader.TryGetField<double>("priceperunit", out double PricePerUnit);
                reader.TryGetField<string>("leasecontractlength", out string LeaseContractLength);
                reader.TryGetField<string>("pricedescription", out string PriceDescription);
                reader.TryGetField<string>("offertermcode", out string OfferTermCode);
                reader.TryGetField<int>("vcpu", out int vCPU);
                reader.TryGetField<string>("memory", out string MemoryString);

                double Memory = 0;

                if (!String.IsNullOrEmpty(MemoryString))
                {
                    MemoryString = MemoryString.Replace(",", "");

                    Match MemoryMatch = _MemoryRegex.Match(MemoryString);

                    if (MemoryMatch.Success)
                    {
                        Double.TryParse(MemoryMatch.Groups[1].Value, out Memory);
                    }
                }

                Term TermType = Term.ON_DEMAND;

                if (reader.TryGetField<string>("termtype", out string TermString))
                {
                    TermType = EnumConverters.ConvertToTerm(TermString);
                }

                if (String.IsNullOrEmpty(InstanceType))
                {
                    // This will probably only happen for DynamoDB
                    InstanceType = UsageType;
                }

                PurchaseOption PurchaseOption = PurchaseOption.ON_DEMAND;

                if (reader.TryGetField<string>("purchaseoption", out string PurchaseOptionString))
                {
                    PurchaseOption = EnumConverters.ConvertToPurchaseOption(PurchaseOptionString);
                }

                OfferingClass OfferingClass = OfferingClass.STANDARD;

                if (reader.TryGetField<string>("offeringclass", out string OfferingClassString))
                {
                    OfferingClass = EnumConverters.ConvertToOfferingClass(OfferingClassString);
                }

                // Only EC2 has tenancy
                if (!reader.TryGetField<string>("tenancy", out string Tenancy))
                {
                    Tenancy = "Shared";
                }

                int Lease = String.IsNullOrEmpty(LeaseContractLength) ? 0 : Int32.Parse(Regex.Match(LeaseContractLength, "^([0-9]+)").Groups[1].Value);

                string Platform = GetPlatform(reader);

                if (!reader.TryGetField<string>("operating system", out string OperatingSystem))
                {
                    if (!String.IsNullOrEmpty(Platform))
                    {
                        OperatingSystem = Platform;
                    }
                    else
                    {
                        OperatingSystem = ServiceCode;
                    }
                }

                return new CsvRowItem(
                    Sku,
                    OfferTermCode,
                    TermType,
                    Lease,
                    PricePerUnit,
                    vCPU,
                    Memory,
                    PurchaseOption,
                    OfferingClass,
                    Tenancy,
                    InstanceType,
                    Platform,
                    OperatingSystem,
                    Operation,
                    UsageType,
                    ServiceCode,
                    RegionMapper.GetRegionFromUsageType(UsageType),
                    PriceDescription
                );
            }
            else
            {
                return null;
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Extracts the platform details from different attribute fields in the price list data
        /// </summary>
        /// <param name="reader"></param>
        /// <returns></returns>
        private static string GetPlatform(CsvReader reader)
        {
            StringBuilder Buffer = new StringBuilder();

            if (reader.TryGetField<string>("servicecode", out string ServiceCode))
            {
                reader.TryGetField("license model", out string LicenseModel);

                switch (ServiceCode.ToLower())
                {
                    case "amazonrds":
                        {
                            reader.TryGetField("database engine", out string DatabaseEngine);
                            reader.TryGetField("database edition", out string DatabaseEdition);

                            Buffer.Append("RDS ").Append(DatabaseEngine);

                            switch (DatabaseEngine.ToLower())
                            {
                                case "sql server":
                                    {
                                        if (!String.IsNullOrEmpty(DatabaseEdition))
                                        {
                                            switch (DatabaseEdition.ToLower())
                                            {
                                                case "enterprise":
                                                    {
                                                        Buffer.Append(" EE");
                                                        break;
                                                    }
                                                case "standard":
                                                    {
                                                        Buffer.Append(" SE");
                                                        break;
                                                    }
                                                case "web":
                                                    {
                                                        Buffer.Append(" Web");
                                                        break;
                                                    }
                                            }
                                        }
                                        break;
                                    }
                                case "oracle":
                                    {
                                        if (!String.IsNullOrEmpty(DatabaseEdition))
                                        {
                                            switch (DatabaseEdition.ToLower())
                                            {
                                                case "enterprise":
                                                    {
                                                        Buffer.Append(" EE");
                                                        break;
                                                    }
                                                case "standard":
                                                    {
                                                        Buffer.Append(" SE");
                                                        break;
                                                    }
                                                case "standard one":
                                                    {
                                                        Buffer.Append(" SE1");
                                                        break;
                                                    }
                                                case "standard two":
                                                    {
                                                        Buffer.Append(" SE2");
                                                        break;
                                                    }
                                            }
                                        }

                                        break;
                                    }
                            }

                            switch (LicenseModel.ToLower())
                            {
                                case "bring your own license":
                                    {
                                        Buffer.Append(" BYOL");
                                        break;
                                    }

                                default:
                                case "license included":
                                case "no license required":
                                    {
                                        break;
                                    }
                            }

                            if (reader.TryGetField("deployment option", out string DeploymentOption) &&
                                DeploymentOption.Equals("Multi-AZ", StringComparison.OrdinalIgnoreCase))
                            {
                                Buffer.Append(" Multi-AZ");
                            }

                            break;
                        }
                    case "amazonec2":
                        {
                            reader.TryGetField("operating system", out string OperatingSystem);
                            Buffer.Append(OperatingSystem);

                            switch (LicenseModel.ToLower())
                            {
                                case "bring your own license":
                                    {
                                        Buffer.Append(" BYOL");
                                        break;
                                    }

                                default:
                                case "license included":
                                case "no license required":
                                    {
                                        break;
                                    }
                            }

                            if (reader.TryGetField("pre installed s/w", out string PreInstalledSW) &&
                                !PreInstalledSW.Equals("NA", StringComparison.OrdinalIgnoreCase))
                            {
                                Buffer.Append(" with ").Append(PreInstalledSW);
                            }

                            break;
                        }
                    case "amazonelasticache":
                        {
                            reader.TryGetField("cache engine", out string CacheEngine);
                            Buffer.Append("ElastiCache");

                            if (!String.IsNullOrEmpty(CacheEngine))
                            {
                                Buffer.Append(" ").Append(CacheEngine);
                            }

                            break;
                        }
                    case "amazondynamodb":
                        {
                            reader.TryGetField("group", out string Group);

                            if (!String.IsNullOrEmpty(Group))
                            {
                                Buffer.Append(Group);
                            }
                            else
                            {
                                Buffer.Append("Amazon DynamoDB");
                            }

                            break;
                        }
                    case "amazonredshift":
                        {
                            reader.TryGetField("usage family", out string UsageFamily);

                            if (!String.IsNullOrEmpty(UsageFamily))
                            {
                                Buffer.Append(UsageFamily);
                            }
                            else
                            {
                                Buffer.Append("Amazon Redshift");
                            }

                            break;
                        }
                    default:
                        {
                            Buffer.Append("UNKNOWN SERVICE ").Append(ServiceCode);
                            break;
                        }
                }

                return Buffer.ToString();
            }
            else
            {
                return "UNKNOWN";
            }
        }

        #endregion
    }
}
