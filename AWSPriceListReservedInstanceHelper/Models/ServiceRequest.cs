using Newtonsoft.Json;

namespace BAMCIS.LambdaFunctions.AWSPriceListReservedInstanceHelper.Models
{
    /// <summary>
    /// A request to get the price list data for the specified service
    /// </summary>
    public class ServiceRequest
    {
        #region Public Properties

        /// <summary>
        /// The service to get price list data for
        /// </summary>
        public string Service { get; }

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new service request
        /// </summary>
        /// <param name="service"></param>
        [JsonConstructor]
        public ServiceRequest(string service)
        {
            this.Service = service;
        }

        #endregion
    }
}
