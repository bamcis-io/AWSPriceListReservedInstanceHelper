using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace BAMCIS.LambdaFunctions.PriceListApiFormatter
{
    public static class RegionMapper
    {
        #region Private Fields

        private static IDictionary<string, string> RegionMap = new Dictionary<string, string>()
        {
            { "", "us-east-1"},
            { "USE2", "us-east-2" },
            { "USW1", "us-west-1" },
            { "USW2", "us-west-2" },
            { "UGW1", "us-gov-west-1" },
            { "UGE1", "us-gov-east-1" },
            { "CAN1", "ca-central-1" },
            { "APN1", "ap-northeast-1" },
            { "APN2", "ap-northeast-2" },
            { "APN3", "ap-northeast-3" },
            { "APS1", "ap-southeast-1" },
            { "APS2", "ap-southeast-2" },
            { "APS3", "ap-south-1" },
            { "SAE1", "sa-east-1" },
            { "EU", "eu-west-1" },
            { "EUC1", "eu-central-1" },
            { "EUW2", "eu-west-2" },
            { "EUW3", "eu-west-3" }
        };

        private static Regex UsageTypeParser = new Regex("^([a-zA-Z]{2,3}[0-9]*)-.*$");

        #endregion

        #region Public Properties

        public static string GetRegionFromUsageType(string usageType)
        {
            return GetRegion(ParseUsageType(usageType));
        }

        public static string ParseUsageType(string usageType)
        {
            Match RegexMatch = UsageTypeParser.Match(usageType);

            if (RegexMatch.Success)
            {
                return RegexMatch.Groups[1].Value;
            }
            else
            {
                return String.Empty;
            }
        }

        public static string GetRegion(string value)
        {
            if (RegionMap.ContainsKey(value))
            {
                return RegionMap[value];
            }
            else
            {
                return String.Empty;
            }
        }

        #endregion
    }
}
