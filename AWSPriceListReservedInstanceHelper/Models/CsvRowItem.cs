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
        private static readonly Regex allUsageTypes = new Regex(@"(?:\bBoxUsage\b|HeavyUsage|DedicatedUsage|NodeUsage|Multi-AZUsage|InstanceUsage|HostBoxUsage|\bNode\b|\bWriteCapacityUnit|\bReadCapacityUnit)", RegexOptions.IgnoreCase);

        /// <summary>
        /// Finds the amount of memory allocated to an instance
        /// This should be used only after removing commas from the input string
        /// </summary>
        private static readonly Regex memoryRegex = new Regex(@"^\s*([0-9]+(?:\.?[0-9]+)?)\s+GiB\s*$", RegexOptions.IgnoreCase);

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
        /// does not change the position of the csv reader. If the row item
        /// does not describe a "reservable" charge, then null is returned
        /// </summary>
        /// <param name="reader">The csv reader to read from</param>
        /// <returns></returns>
        public static CsvRowItem Build(CsvReader reader)
        {
            string instanceType = String.Empty;

            // The field names are case sensitive
            if (reader.TryGetField<string>("operation", out string operation)
               && !String.IsNullOrEmpty(operation)
               && reader.TryGetField<string>("usagetype", out string usageType)
               && allUsageTypes.IsMatch(usageType)
               && reader.TryGetField<string>("servicecode", out string serviceCode)
               && !String.IsNullOrEmpty(serviceCode)
               && (Constants.InstanceBasedReservableServices.Contains(serviceCode) ? reader.TryGetField("instance type", out instanceType) : true)
           )
            {
                reader.TryGetField<string>("sku", out string sku);
                reader.TryGetField<double>("priceperunit", out double pricePerUnit);
                reader.TryGetField<string>("leasecontractlength", out string leaseContractLength);
                reader.TryGetField<string>("pricedescription", out string priceDescription);
                reader.TryGetField<string>("offertermcode", out string offerTermCode);
                reader.TryGetField<int>("vcpu", out int vCPU);
                reader.TryGetField<string>("memory", out string memoryString);

                double memory = 0;

                if (!String.IsNullOrEmpty(memoryString))
                {
                    memoryString = memoryString.Replace(",", "");

                    Match memoryMatch = memoryRegex.Match(memoryString);

                    if (memoryMatch.Success)
                    {
                        Double.TryParse(memoryMatch.Groups[1].Value, out memory);
                    }
                }

                Term termType = Term.ON_DEMAND;

                if (reader.TryGetField<string>("termtype", out string termString))
                {
                    termType = EnumConverters.ConvertToTerm(termString);
                }

                if (String.IsNullOrEmpty(instanceType))
                {
                    // This will probably only happen for DynamoDB
                    instanceType = usageType;
                }

                PurchaseOption purchaseOption = PurchaseOption.ON_DEMAND;

                if (reader.TryGetField<string>("purchaseoption", out string PurchaseOptionString))
                {
                    purchaseOption = EnumConverters.ConvertToPurchaseOption(PurchaseOptionString);
                }

                OfferingClass offeringClass = OfferingClass.STANDARD;

                if (reader.TryGetField<string>("offeringclass", out string offeringClassString))
                {
                    offeringClass = EnumConverters.ConvertToOfferingClass(offeringClassString);
                }

                // Only EC2 has tenancy
                if (!reader.TryGetField<string>("tenancy", out string tenancy))
                {
                    tenancy = "Shared";
                }

                int lease = String.IsNullOrEmpty(leaseContractLength) ? 0 : Int32.Parse(Regex.Match(leaseContractLength, "^([0-9]+)").Groups[1].Value);

                string platform = GetPlatform(reader);

                if (!reader.TryGetField<string>("operating system", out string operatingSystem))
                {
                    if (!String.IsNullOrEmpty(platform))
                    {
                        operatingSystem = platform;
                    }
                    else
                    {
                        operatingSystem = serviceCode;
                    }
                }

                return new CsvRowItem(
                    sku,
                    offerTermCode,
                    termType,
                    lease,
                    pricePerUnit,
                    vCPU,
                    memory,
                    purchaseOption,
                    offeringClass,
                    tenancy,
                    instanceType,
                    platform,
                    operatingSystem,
                    operation,
                    usageType,
                    serviceCode,
                    RegionMapper.GetRegionFromUsageType(usageType),
                    priceDescription
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
            StringBuilder buffer = new StringBuilder();

            if (reader.TryGetField<string>("servicecode", out string serviceCode))
            {
                reader.TryGetField("license model", out string licenseModel);

                switch (serviceCode.ToLower())
                {
                    case "amazonrds":
                        {
                            reader.TryGetField("database engine", out string databaseEngine);
                            reader.TryGetField("database edition", out string databaseEdition);

                            buffer.Append("RDS ").Append(databaseEngine);

                            switch (databaseEngine.ToLower())
                            {
                                case "sql server":
                                    {
                                        if (!String.IsNullOrEmpty(databaseEdition))
                                        {
                                            switch (databaseEdition.ToLower())
                                            {
                                                case "enterprise":
                                                    {
                                                        buffer.Append(" EE");
                                                        break;
                                                    }
                                                case "standard":
                                                    {
                                                        buffer.Append(" SE");
                                                        break;
                                                    }
                                                case "web":
                                                    {
                                                        buffer.Append(" Web");
                                                        break;
                                                    }
                                            }
                                        }
                                        break;
                                    }
                                case "oracle":
                                    {
                                        if (!String.IsNullOrEmpty(databaseEdition))
                                        {
                                            switch (databaseEdition.ToLower())
                                            {
                                                case "enterprise":
                                                    {
                                                        buffer.Append(" EE");
                                                        break;
                                                    }
                                                case "standard":
                                                    {
                                                        buffer.Append(" SE");
                                                        break;
                                                    }
                                                case "standard one":
                                                    {
                                                        buffer.Append(" SE1");
                                                        break;
                                                    }
                                                case "standard two":
                                                    {
                                                        buffer.Append(" SE2");
                                                        break;
                                                    }
                                            }
                                        }

                                        break;
                                    }
                            }

                            switch (licenseModel.ToLower())
                            {
                                case "bring your own license":
                                    {
                                        buffer.Append(" BYOL");
                                        break;
                                    }

                                default:
                                case "license included":
                                case "no license required":
                                    {
                                        break;
                                    }
                            }

                            if (reader.TryGetField("deployment option", out string deploymentOption) &&
                                deploymentOption.Equals("Multi-AZ", StringComparison.OrdinalIgnoreCase))
                            {
                                buffer.Append(" Multi-AZ");
                            }

                            break;
                        }
                    case "amazonec2":
                        {
                            reader.TryGetField("operating system", out string operatingSystem);
                            buffer.Append(operatingSystem);

                            switch (licenseModel.ToLower())
                            {
                                case "bring your own license":
                                    {
                                        buffer.Append(" BYOL");
                                        break;
                                    }

                                default:
                                case "license included":
                                case "no license required":
                                    {
                                        break;
                                    }
                            }

                            if (reader.TryGetField("pre installed s/w", out string preInstalledSW) &&
                                !preInstalledSW.Equals("NA", StringComparison.OrdinalIgnoreCase))
                            {
                                buffer.Append(" with ").Append(preInstalledSW);
                            }

                            break;
                        }
                    case "amazonelasticache":
                        {
                            reader.TryGetField("cache engine", out string cacheEngine);
                            buffer.Append("ElastiCache");

                            if (!String.IsNullOrEmpty(cacheEngine))
                            {
                                buffer.Append(" ").Append(cacheEngine);
                            }

                            break;
                        }
                    case "amazondynamodb":
                        {
                            reader.TryGetField("group", out string group);

                            if (!String.IsNullOrEmpty(group))
                            {
                                buffer.Append(group);
                            }
                            else
                            {
                                buffer.Append("Amazon DynamoDB");
                            }

                            break;
                        }
                    case "amazonredshift":
                        {
                            reader.TryGetField("usage family", out string usageFamily);

                            if (!String.IsNullOrEmpty(usageFamily))
                            {
                                buffer.Append(usageFamily);
                            }
                            else
                            {
                                buffer.Append("Amazon Redshift");
                            }

                            break;
                        }
                    case "amazones":
                        {
                            buffer.Append("Amazon Elasticsearch");
                            break;
                        }
                    default:
                        {
                            buffer.Append("UNKNOWN SERVICE ").Append(serviceCode);
                            break;
                        }
                }

                return buffer.ToString();
            }
            else
            {
                return "UNKNOWN";
            }
        }

        #endregion
    }
}
