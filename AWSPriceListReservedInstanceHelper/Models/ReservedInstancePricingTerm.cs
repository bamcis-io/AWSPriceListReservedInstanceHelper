using BAMCIS.AWSPriceListApi.Serde;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace BAMCIS.LambdaFunctions.AWSPriceListReservedInstanceHelper.Models
{
    /// <summary>
    /// This class represents the combination of multiple price dimensions for a single
    /// product so we end up combining the upfront fee and recurring fee price dimensions
    /// into a single set of information
    /// </summary>
    public sealed class ReservedInstancePricingTerm
    {
        #region Private Fields

        /// <summary>
        /// Will hold any error messages generated during creating the processing 
        /// of the reserved instance pricing terms
        /// </summary>
        private List<string> ErrorMessages = new List<string>();

        // This should be used only after removing commas from the input string
        private static readonly Regex _MemoryRegex = new Regex(@"^\s*([0-9]+(?:\.?[0-9]+)?)\s+GiB\s*$", RegexOptions.IgnoreCase);

        // The free tier string in the DynamoDB price dimension description
        private static readonly string FREE_TIER = "(free tier)";

        #endregion

        #region Public Properties

        /// <summary>
        /// The SKU that matches a SKU from the products section, this is the key of the term item
        /// </summary>
        public string Sku { get; }

        /// <summary>
        /// The code specific to the offer term, the key for the term is SKU.OfferTermCode, like 76V3SF2FJC3ZR3GH.JRTCKXETXF
        /// </summary>
        public string OfferTermCode { get; }

        /// <summary>
        /// The platform of the product, like Linux, RHEL, MySQL, SQL Server Web BYOL, Oracle EE, ElastiCache Redis, etc
        /// </summary>
        public string Platform { get; }

        /// <summary>
        /// The tenancy of the instance
        /// </summary>
        public string Tenancy { get; }

        /// <summary>
        /// The operation code for the price term
        /// </summary>
        public string Operation { get; }

        /// <summary>
        /// The usage type for the price term
        /// </summary>
        public string UsageType { get; }

        /// <summary>
        /// The region for the pricing term
        /// </summary>
        public string Region { get; }

        /// <summary>
        /// The service family this price term relates to, like Amazon Elastic Compute Cloud
        /// </summary>
        public string Service { get; }

        /// <summary>
        /// The instance type for this pricing term
        /// </summary>
        public string InstanceType { get; }

        /// <summary>
        /// The operating system running on the instance
        /// </summary>
        public string OperatingSystem { get; }

        /// <summary>
        /// The price per unit for the resource, typically the hourly price, 
        /// will be 0 for All Upfront reserved instances. This is different than 
        /// the on-demand hourly price for reserved instance, i.e. it will be 
        /// their recurring fee for a no upfront or partial upfront instance.
        /// </summary>
        public double AdjustedPricePerUnit { get; }

        /// <summary>
        /// The normal on demand cost for running the instance
        /// </summary>
        public double OnDemandHourlyCost { get; }

        /// <summary>
        /// A percentage, between 0 and 1, representing the amount of time during a 
        /// lease period that an instance would need to be running for the reserved instance
        /// option to cost less. For example, if it was .695 for a 1 year lease, if the instance
        /// ran less than 69.5% of the time during the year, it would be cheaper to pay the on
        /// demand costs than buy a reserved instance
        /// </summary>
        public double BreakevenPercentage { get; }

        /// <summary>
        /// This is the upfront fee associated with the term, will be 0 for on-demand resources
        /// </summary>
        public double UpfrontFee { get; }

        /// <summary>
        /// The length of the reserved instance term, usually 1 or 3 years
        /// </summary>
        public int LeaseTerm { get; }

        /// <summary>
        /// The purchasing option for the reserved instance, may be no upfront,
        /// all upfront, heavy utilization, light utilization, etc
        /// </summary>
        public PurchaseOption PurchaseOption { get; }

        /// <summary>
        /// The offering class of the reserved instance, like standard or convertible
        /// </summary>
        public OfferingClass OfferingClass { get; }

        /// <summary>
        /// The type of the term
        /// </summary>
        public Term TermType { get; }

        /// <summary>
        /// The unique key identifying this term among the other pricing terms
        /// that share the same sku
        /// </summary>
        public string Key { get; }

        /// <summary>
        /// The total cost of the reserved instance including upfront and recurring fees for the lease term
        /// </summary>
        public double ReservedInstanceCost { get; }

        /// <summary>
        /// The total cost running the instance on demand for the lease term would cost
        /// </summary>
        public double OnDemandCostForTerm { get; }

        /// <summary>
        /// The maximum total cost savings of using the RI over running on-demand. This assumes
        /// 24/7 utilization for the entire lease term.
        /// </summary>
        public double CostSavings { get; }

        /// <summary>
        /// The maximum savings from using the reserved instance as a percent over the on demand costs
        /// </summary>
        public double PercentSavings { get; }

        /// <summary>
        /// The number of vCPUs for the instance
        /// </summary>
        public int vCPU { get; }

        /// <summary>
        /// The amount of RAM on the instance, measured in GiB
        /// </summary>
        public double Memory { get; }

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new reserved instance pricing term
        /// </summary>
        /// <param name="sku"></param>
        /// <param name="offerTermCode"></param>
        /// <param name="service"></param>
        /// <param name="platform"></param>
        /// <param name="operatingSystem"></param>
        /// <param name="instanceType"></param>
        /// <param name="operation"></param>
        /// <param name="usageType"></param>
        /// <param name="tenancy"></param>
        /// <param name="region"></param>
        /// <param name="vcpus"></param>
        /// <param name="memory"></param>
        /// <param name="onDemandHourlyCost"></param>
        /// <param name="adjustedPricePerUnit"></param>
        /// <param name="upfrontFee"></param>
        /// <param name="leaseTerm"></param>
        /// <param name="purchaseOption"></param>
        /// <param name="offeringClass"></param>
        /// <param name="termType"></param>
        [JsonConstructor]
        public ReservedInstancePricingTerm(
            string sku,
            string offerTermCode,
            string service,
            string platform,
            string operatingSystem,
            string instanceType,
            string operation,
            string usageType,
            string tenancy,
            string region,
            int vcpus,
            double memory,
            double onDemandHourlyCost,
            double adjustedPricePerUnit,
            double upfrontFee,
            int leaseTerm,
            PurchaseOption purchaseOption,
            OfferingClass offeringClass,
            Term termType
        )
        {
            this.Sku = sku;
            this.OfferTermCode = offerTermCode;
            this.Service = service;
            this.Platform = platform;
            this.OperatingSystem = operatingSystem;
            this.InstanceType = instanceType;
            this.Operation = operation;
            this.UsageType = usageType;
            this.Tenancy = tenancy;
            this.Region = region;
            this.vCPU = vcpus;
            this.Memory = memory;
            this.OnDemandHourlyCost = onDemandHourlyCost;
            this.AdjustedPricePerUnit = adjustedPricePerUnit;
            this.UpfrontFee = upfrontFee;
            this.LeaseTerm = leaseTerm;
            this.PurchaseOption = purchaseOption;
            this.OfferingClass = offeringClass;
            this.TermType = termType;

            this.BreakevenPercentage = (this.UpfrontFee + (365 * this.LeaseTerm * 24 * this.AdjustedPricePerUnit)) / (365 * this.LeaseTerm * 24 * this.OnDemandHourlyCost);

            if (termType == Term.ON_DEMAND)
            {
                this.Key = "OnDemand";
            }
            else
            {
                this.Key = $"{this.LeaseTerm}::{this.PurchaseOption.ToString()}::{this.OfferingClass.ToString()}";
            }

            // Calculated properties

            this.ReservedInstanceCost = this.UpfrontFee + (this.AdjustedPricePerUnit * 24 * 365 * this.LeaseTerm);
            this.OnDemandCostForTerm = this.OnDemandHourlyCost * 24 * 365 * this.LeaseTerm;
            this.CostSavings = OnDemandCostForTerm - ReservedInstanceCost;

            if (this.OnDemandCostForTerm > 0)
            {
                this.PercentSavings = Math.Round(((1 - (ReservedInstanceCost / OnDemandCostForTerm)) * 100), 3);
            }
            else
            {
                this.PercentSavings = 0;
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Creates a consolidated pricing term from a regularly formatted pricing term
        /// that comes directly from the price list api. Used when you've grouped the price list by
        /// SKU which gives pricing terms for all on demand and reserved terms in the group. This
        /// will identify the on demand term and then construct the reserved terms.
        /// </summary>
        /// <param name="commonSkus"></param>
        /// <param name="product"></param>
        /// <returns></returns>
        public static IEnumerable<ReservedInstancePricingTerm> Build(IGrouping<string, PricingTerm> commonSkus, Product product)
        {
            if (commonSkus == null)
            {
                throw new ArgumentNullException("commonSkus");
            }

            if (product == null)
            {
                throw new ArgumentNullException("product");
            }

            PricingTerm onDemand = commonSkus.FirstOrDefault(x => x.TermAttributes.PurchaseOption == PurchaseOption.ON_DEMAND);

            if (onDemand == null)
            {
                throw new KeyNotFoundException($"{product.ProductFamily} - An on demand price data term was not found for sku: {commonSkus.Key}.");
            }

            IEnumerable<PricingTerm> reservedTerms = commonSkus.Where(x => x.TermAttributes.PurchaseOption != PurchaseOption.ON_DEMAND);

            if (!reservedTerms.Any())
            {
                return Enumerable.Empty<ReservedInstancePricingTerm>();
            }

            return Build(product, onDemand, reservedTerms);
        }

        /// <summary>
        /// Creates a consolidated pricing term from price list csv rows that share a common sku
        /// </summary>
        /// <param name="commonSkus"></param>
        /// <returns></returns>
        public static IEnumerable<ReservedInstancePricingTerm> BuildFromCsv(IGrouping<string, CsvRowItem> commonSkus)
        {
            return BuildFromCsv(new KeyValuePair<string, List<CsvRowItem>>(commonSkus.Key, commonSkus.ToList()));
        }

        /// <summary>
        /// Creates a consolidated pricing term from price list csv rows that share a common sku
        /// </summary>
        /// <param name="commonSkus"></param>
        /// <returns></returns>
        public static IEnumerable<ReservedInstancePricingTerm> BuildFromCsv(KeyValuePair<string, List<CsvRowItem>> commonSkus)
        {
            if (commonSkus.Value != null)
            {
                // This probably needs a better check for whether a csv row item represents on demand
                // free tier charges.
                // Amazon DynamoDB - HBHVWY47CM => Free tier
                // EC2, Redshift, RDS, ElastiCache do not include free tier charge dimensions in price
                // list file as of 9/25/2018
                CsvRowItem onDemandRow = commonSkus.Value.FirstOrDefault(x => x.PurchaseOption == PurchaseOption.ON_DEMAND && !x.Description.Contains(FREE_TIER, StringComparison.OrdinalIgnoreCase));
                double onDemandCost = -1;

                if (onDemandRow == null)
                {
                    throw new KeyNotFoundException($"{(commonSkus.Value.First() != null ? commonSkus.Value.First().ServiceCode : "UNKNOWN")} - An on demand price data term was not found for sku: {commonSkus.Key}.");
                }
                else
                {
                    onDemandCost = onDemandRow.PricePerUnit;
                }

                // Group based on key because there will be 2 items for 
                // each "key" in the csv, one line item for the recurring
                // and one for the upfront fee
                foreach (IGrouping<string, CsvRowItem> group in commonSkus.Value.Where(x => x.PurchaseOption != PurchaseOption.ON_DEMAND).GroupBy(x => x.Key))
                {
                    CsvRowItem upfront = group.FirstOrDefault(x => x.Description.Equals("upfront fee", StringComparison.OrdinalIgnoreCase));

                    CsvRowItem recurring = group.FirstOrDefault(x => !x.Description.Equals("upfront fee", StringComparison.OrdinalIgnoreCase));

                    double hourlyRecurring = 0;

                    // Only check for recurring, since some may have no upfront
                    if (recurring == null)
                    {
                        // This should never happen
                        throw new KeyNotFoundException($"The pricing term in {group.First().ServiceCode} for sku {group.First().Sku} and offer term code {group.First().OfferTermCode} did not contain a price dimension for hourly usage charges.");
                    }
                    else
                    {
                        hourlyRecurring = recurring.PricePerUnit;
                    }

                    double upfrontFee = 0;

                    if (upfront != null)
                    {
                        upfrontFee = upfront.PricePerUnit;
                    }

                    CsvRowItem first = group.First();

                    yield return new ReservedInstancePricingTerm(
                        first.Sku,
                        first.OfferTermCode,
                        first.ServiceCode,
                        first.Platform,
                        first.OperatingSystem,
                        first.InstanceType,
                        first.Operation,
                        first.UsageType,
                        first.Tenancy,
                        first.Region,
                        first.vCPU,
                        first.Memory,
                        onDemandCost,
                        hourlyRecurring,
                        upfrontFee,
                        first.LeaseContractLength,
                        first.PurchaseOption,
                        first.OfferingClass,
                        Term.RESERVED
                    );
                }
            }
            else
            {
                throw new ArgumentNullException("commonSkus");
            }
        }

        /// <summary>
        /// Creates a consolidated pricing term from a regularly formatted pricing term
        /// that comes directly from the price list API. Used when you've already identified the on
        /// demand pricing term and have separated out just the reserved pricing terms.
        /// </summary>
        /// <param name="product"></param>
        /// <param name="ondemand"></param>
        /// <param name="reservedterms"></param>
        /// <returns></returns>
        public static IEnumerable<ReservedInstancePricingTerm> Build(Product product, PricingTerm ondemand, IEnumerable<PricingTerm> reservedterms)
        {
            if (product == null)
            {
                throw new ArgumentNullException("product");
            }

            if (ondemand == null)
            {
                throw new ArgumentNullException("ondemand");
            }

            if (reservedterms == null)
            {
                throw new ArgumentNullException("reservedterms");
            }

            if (!reservedterms.Any())
            {
                throw new ArgumentException("You must supply at least 1 reserved term.");
            }

            double onDemandCost = -1;

            // Get the on demand hourly cost
            // DynamoDB has free tier price dimensions in the same on demand object, so make 
            // sure we pick the first that does not have free tier in the description
            KeyValuePair<string, PriceDimension> dimension = ondemand.PriceDimensions.FirstOrDefault(x => !x.Value.Description.Contains(FREE_TIER, StringComparison.OrdinalIgnoreCase));

            if (dimension.Value == null)
            {
                onDemandCost = 0;
            }
            else if (!Double.TryParse(dimension.Value.PricePerUnit.First().Value, out onDemandCost))
            {
                throw new FormatException($"Could not parse the on demand price {dimension.Value.PricePerUnit.First().Value} for sku: {product.Sku}.");
            }

            string platform = GetPlatform(product);
            string region = RegionMapper.GetRegionFromUsageType(product.Attributes.GetValueOrDefault("usagetype"));

            // Only EC2 has tenancy
            if (!product.Attributes.TryGetValue("tenancy", out string tenancy))
            {
                tenancy = "Shared";
            }

            // Each pricing term will have the price dimensions for the upfront and recurring costs
            foreach (PricingTerm term in reservedterms)
            {
                PriceDimension upfront = term.PriceDimensions
                    .Select(x => x.Value)
                    .FirstOrDefault(x => !String.IsNullOrEmpty(x.Description) && x.Description.Equals("upfront fee", StringComparison.OrdinalIgnoreCase));

                PriceDimension recurring = term.PriceDimensions
                    .Select(x => x.Value)
                    .FirstOrDefault(x => !String.IsNullOrEmpty(x.Description) && !x.Description.Equals("upfront fee", StringComparison.OrdinalIgnoreCase));

                double hourlyRecurring = 0;

                // Only check for recurring, since some may have no upfront
                if (recurring == null)
                {
                    // This should never happen
                    throw new KeyNotFoundException($"The pricing term in {product.Attributes.GetValueOrDefault("servicecode")} for sku {term.Sku} and offer term code {term.OfferTermCode} did not contain a price dimension for hourly usage charges.");
                }
                else
                {
                    // Parse out the rate
                    if (!Double.TryParse(recurring.PricePerUnit.First().Value, out hourlyRecurring))
                    {
                        throw new FormatException($"Could not parse the recurring price per unit of {recurring.PricePerUnit.First().Value} for sku {term.Sku}, offer term code {term.OfferTermCode}, in service {product.Attributes.GetValueOrDefault("servicecode")}.");
                    }
                }

                double upfrontFee = 0;

                if (upfront != null)
                {
                    // Parse out upfront fee
                    if (!Double.TryParse(upfront.PricePerUnit.First().Value, out upfrontFee))
                    {
                        throw new FormatException($"Could not parse the upfront cost of {upfront.PricePerUnit.First().Value} for sku {term.Sku}, offer term code {term.OfferTermCode}, in service {product.Attributes.GetValueOrDefault("servicecode")}.");
                    }
                }

                string operatingSystem = String.Empty;

                if (product.Attributes.ContainsKey("operatingsystem"))
                {
                    operatingSystem = product.Attributes.GetValueOrDefault("operatingsystem");
                }
                else
                {
                    operatingSystem = platform;
                }

                int vCPU = 0;

                if (product.Attributes.ContainsKey("vcpu"))
                {
                    Int32.TryParse(product.Attributes.GetValueOrDefault("vcpu"), out vCPU);
                }

                double memory = 0;

                if (product.Attributes.ContainsKey("memory"))
                {
                    string memoryString = product.Attributes.GetValueOrDefault("memory").Replace(",", "");

                    Match memoryMatch = _MemoryRegex.Match(memoryString);

                    if (memoryMatch.Success)
                    {
                        Double.TryParse(memoryMatch.Groups[1].Value, out memory);
                    }
                }

                string usageType = product.Attributes.GetValueOrDefault("usagetype");

                string instanceType = usageType;

                if (product.Attributes.ContainsKey("instancetype"))
                {
                    instanceType = product.Attributes.GetValueOrDefault("usagetype");
                }

                yield return new ReservedInstancePricingTerm(
                    term.Sku,
                    term.OfferTermCode,
                    product.Attributes.GetValueOrDefault("servicecode"),
                    platform,
                    operatingSystem,
                    instanceType,
                    product.Attributes.GetValueOrDefault("operation"),
                    usageType,
                    tenancy,
                    region,
                    vCPU,
                    memory,
                    onDemandCost,
                    hourlyRecurring,
                    upfrontFee,
                    term.TermAttributes.LeaseContractLength,
                    term.TermAttributes.PurchaseOption,
                    term.TermAttributes.OfferingClass,
                    AWSPriceListApi.Serde.Term.RESERVED
                );
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Gets the platform string from the product information
        /// </summary>
        /// <param name="product"></param>
        /// <returns>The platform string or UNKNOWN if it couldn't be determined</returns>
        private static string GetPlatform(Product product)
        {
            StringBuilder buffer = new StringBuilder();
            IDictionary<string, string> attributes = product.Attributes.ToDictionary(x => x.Key.ToLower(), x => x.Value, StringComparer.OrdinalIgnoreCase);

            if (attributes.KeyExistsAndValueNotNullOrEmpty("servicecode"))
            {
                attributes.TryGetValue("servicecode", out string serviceCode);
                attributes.TryGetValue("licensemodel", out string licenseModel);

                switch (serviceCode.ToLower())
                {
                    case "amazonrds":
                        {
                            attributes.TryGetValue("databaseengine", out string databaseEngine);
                            attributes.TryGetValue("databaseedition", out string databaseEdition);

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

                            if (attributes.TryGetValue("deploymentoption", out string deploymentOption) &&
                                deploymentOption.Equals("Multi-AZ", StringComparison.OrdinalIgnoreCase))
                            {
                                buffer.Append(" Multi-AZ");
                            }

                            break;
                        }
                    case "amazonec2":
                        {
                            attributes.TryGetValue("operatingsystem", out string OperatingSystem);
                            buffer.Append(OperatingSystem);

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

                            if (attributes.TryGetValue("preinstalledsw", out string preInstalledSW) &&
                                !preInstalledSW.Equals("NA", StringComparison.OrdinalIgnoreCase))
                            {
                                buffer.Append(" with ").Append(preInstalledSW);
                            }

                            break;
                        }
                    case "amazonelasticache":
                        {                            
                            buffer.Append("ElastiCache");

                            if (attributes.TryGetValue("cacheengine", out string cacheEngine) && !String.IsNullOrEmpty(cacheEngine))
                            {
                                buffer.Append(" ").Append(cacheEngine);
                            }

                            break;
                        }
                    case "amazones":
                        {
                            buffer.Append("Amazon Elasticsearch");
                            break;
                        }
                    case "amazondynamodb":
                        {
                           
                            if (attributes.TryGetValue("group", out string group) &&
                             !String.IsNullOrEmpty(group))
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
                            if (attributes.TryGetValue("usageFamily", out string usageFamily) && 
                                !String.IsNullOrEmpty(usageFamily))
                            {
                                buffer.Append(usageFamily);
                            }
                            else
                            {
                                buffer.Append("Amazon Redshift");
                            }

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
