using BAMCIS.AWSPriceListApi.Serde;
using CsvHelper;
using System;
using System.Text;
using System.Text.RegularExpressions;

namespace BAMCIS.LambdaFunctions.PriceListApiFormatter.Models
{
    public class CsvRowItem
    {
        #region Private Fields

        private static readonly Regex _AllUsageTypes = new Regex(@"(?:\bBoxUsage\b|HeavyUsage|DedicatedUsage|NodeUsage|Multi-AZUsage|InstanceUsage|HostBoxUsage)", RegexOptions.IgnoreCase);

        #endregion

        #region Public Properties

        public string Sku { get; }

        public string OfferTermCode { get; }

        public Term TermType { get; }

        public int LeaseContractLength { get; }

        public double PricePerUnit { get; }

        public PurchaseOption PurchaseOption { get; }

        public OfferingClass OfferingClass { get; }

        public string Tenancy { get; }

        public string InstanceType { get; }

        public string Platform { get; }

        public string OperatingSystem { get; }

        public string Operation { get; }

        public string UsageType { get; }

        public string ServiceCode { get; }

        public string Region { get; }

        public string Description { get; }

        public string Key { get; }

        #endregion

        #region Constructors

        public CsvRowItem(
            string sku,
            string offerTermCode,
            Term termType,
            int leaseContractLength,
            double pricePerUnit,
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

        public static CsvRowItem Build(CsvReader reader)
        {
            if (reader.TryGetField<string>("instance type", out string InstanceType)
               && !String.IsNullOrEmpty(InstanceType)
               && reader.TryGetField<string>("usagetype", out string UsageType)
               && _AllUsageTypes.IsMatch(UsageType)
               && reader.TryGetField<string>("servicecode", out string ServiceCode)
               && !String.IsNullOrEmpty(ServiceCode)
           )
            {
                reader.TryGetField<string>("sku", out string Sku);
                reader.TryGetField<double>("priceperunit", out double PricePerUnit);
                reader.TryGetField<string>("leasecontractlength", out string LeaseContractLength);
                reader.TryGetField<string>("pricedescription", out string PriceDescription);
                reader.TryGetField<string>("offertermcode", out string OfferTermCode);

                Term TermType = Term.ON_DEMAND;

                if (reader.TryGetField<string>("term type", out string TermString))
                {
                    TermType = EnumConverters.ConvertToTerm(TermString);
                }

                PurchaseOption PurchaseOption = PurchaseOption.ON_DEMAND;

                if (reader.TryGetField<string>("purchase option", out string PurchaseOptionString))
                {
                    PurchaseOption = EnumConverters.ConvertToPurchaseOption(PurchaseOptionString);
                }

                OfferingClass OfferingClass = OfferingClass.STANDARD;

                if (reader.TryGetField<string>("offering class", out string OfferingClassString))
                {
                    OfferingClass = EnumConverters.ConvertToOfferingClass(OfferingClassString);
                }

                // Only EC2 has tenancy
                if (!reader.TryGetField<string>("tenancy", out string Tenancy))
                {
                    Tenancy = "Shared";
                }

                reader.TryGetField<string>("operation", out string Operation);

                int Lease = String.IsNullOrEmpty(LeaseContractLength) ? 0 : Int32.Parse(Regex.Match(LeaseContractLength, "^([0-9]+)").Groups[1].Value);

                string Platform = GetPlatform(reader);

                if (!reader.TryGetField<string>("operating system", out string OperatingSystem))
                {
                    OperatingSystem = Platform;
                }

                return new CsvRowItem(
                    Sku,
                    OfferTermCode,
                    TermType,
                    Lease,
                    PricePerUnit,
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
                            Buffer.Append("ElastiCache ").Append(CacheEngine);
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
