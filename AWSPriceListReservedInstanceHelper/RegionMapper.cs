using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace BAMCIS.LambdaFunctions.AWSPriceListReservedInstanceHelper
{
    /// <summary>
    /// Performs mapping of usage type strings to the common region strings like
    /// us-east-1. The price list only contains the long names of the Regions like
    /// US East (N. Virginia) which are sometimes inconsistent (i.e. the 
    /// Amazon.RegionEndpoint.USEast1 property returns US East (Virginia) without
    /// the "N."
    /// </summary>
    public static class RegionMapper
    {
        #region Private Fields

        /// <summary>
        /// The map of usage type prefix strings to the region strings
        /// </summary>
        private static IDictionary<string, string> regionMap = new Dictionary<string, string>()
        {
            { "", "us-east-1"},
            { "USE1", "us-east-1"},
            { "USE2", "us-east-2" },
            { "USW1", "us-west-1" },
            { "USW2", "us-west-2" },
            { "UGW1", "us-gov-west-1" },
            { "UGE1", "us-gov-east-1" },
            { "CAN1", "ca-central-1" },
            { "AFS1", "af-south-1" },
            { "APN1", "ap-northeast-1" },
            { "APN2", "ap-northeast-2" },
            { "APN3", "ap-northeast-3" },
            { "APS1", "ap-southeast-1" },
            { "APS2", "ap-southeast-2" },
            { "APS3", "ap-south-1" },
            { "APE1", "ap-east-1" },
            { "SAE1", "sa-east-1" },
            { "EUC1", "eu-central-1" },
            { "EU", "eu-west-1" },
            { "EUW2", "eu-west-2" },
            { "EUW3", "eu-west-3" },
            { "EUN1", "eu-north-1" },
            { "MES1", "me-south-1" },
            { "CNN1", "cn-north1" },
            { "CNN2", "cn-northwest-1" }
        };

        /// <summary>
        /// The regex to parse out the regional prefix code from the usage type string
        /// </summary>
        private static Regex UsageTypeParser = new Regex("^([a-zA-Z]{2,3}[0-9]*)-.*$");

        #endregion

        #region Public Methods

        /// <summary>
        /// Retrieves the region of usage from the usage type string
        /// </summary>
        /// <param name="usageType"></param>
        /// <returns></returns>
        public static string GetRegionFromUsageType(string usageType)
        {
            return GetRegion(ParseUsageType(usageType));
        }

        #endregion

        #region Private Functions

        /// <summary>
        /// Gets the matching regional information from a usage type string
        /// </summary>
        /// <param name="usageType"></param>
        /// <returns></returns>
        private static string ParseUsageType(string usageType)
        {
            Match regexMatch = UsageTypeParser.Match(usageType);

            if (regexMatch.Success)
            {
                return regexMatch.Groups[1].Value;
            }
            else
            {
                return String.Empty;
            }
        }

        /// <summary>
        /// Retrieves the region from the dictionary based on the matching usage type prefix string
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        private static string GetRegion(string value)
        {
            if (regionMap.ContainsKey(value))
            {
                return regionMap[value];
            }
            else
            {
                return String.Empty;
            }
        }

        #endregion
    }
}
