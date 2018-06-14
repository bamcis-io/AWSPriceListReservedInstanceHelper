using System;
using System.Collections.Generic;
using System.Text;

namespace BAMCIS.LambdaFunctions.PriceListApiFormatter
{
    public class PriceData
    {
        public string SKU { get; set; }

        public string Service { get; set; }

        public string Region { get; set; }

        public string InstanceType { get; set; }

        public double PricePerUnit { get; set; }

        public int LeaseContractLength { get; set; }

        public string PurchaseOption { get; set; }

        public string OfferingClass { get; set; }

        public string Platform { get; set; }

        public string Tenancy { get; set; }

        public string UsageType { get; set; }

        public string Operation { get; set; }

        public string TermType { get; set; }
    }
}
