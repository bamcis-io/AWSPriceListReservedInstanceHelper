using System;
using System.Collections.Generic;
using System.Text;

namespace BAMCIS.LambdaFunctions.AWSPriceListReservedInstanceHelper
{
    public static class Constants
    {
        public static IReadOnlyCollection<string> ReservableServices = new string[] { "AmazonEC2", "AmazonRDS", "AmazonElastiCache", "AmazonRedshift", "AmazonDynamoDB", "AmazonES" };

        public static IReadOnlyCollection<string> InstanceBasedReservableServices = new string[] { "AmazonEC2", "AmazonRDS", "AmazonElastiCache", "AmazonRedshift", "AmazonES" };
    }
}
