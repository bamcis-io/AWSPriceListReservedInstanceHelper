using Amazon.Lambda.TestUtilities;
using Amazon;
using System;
using System.Threading.Tasks;
using Xunit;
using BAMCIS.LambdaFunctions.AWSPriceListReservedInstanceHelper.Models;
using BAMCIS.LambdaFunctions.AWSPriceListReservedInstanceHelper;

namespace AWSPriceListReservedInstanceHelper.Tests
{
    public class FunctionTest
    {
        public FunctionTest()
        {
        }

        [Fact]
        public async Task TestEntrypointCsv()
        {
            // ARRANGE

            AWSConfigs.AWSProfilesLocation = $"{Environment.GetEnvironmentVariable("UserProfile")}\\.aws\\credentials";
            Environment.SetEnvironmentVariable("BUCKET", $"{Environment.UserName}-pricelist");
           
            TestLambdaLogger TestLogger = new TestLambdaLogger();
            TestClientContext ClientContext = new TestClientContext();

            TestLambdaContext Context = new TestLambdaContext()
            {
                FunctionName = "PriceListApiFormatter",
                FunctionVersion = "1",
                Logger = TestLogger,
                ClientContext = ClientContext
            };

            ServiceRequest SR = new ServiceRequest("AmazonEC2");

            Entrypoint Ep = new Entrypoint();

            // ACT
            await Ep.Exec(SR, Context);

            // ASSERT
        }

        [Fact]
        public async Task TestEntrypointJson()
        {
            // ARRANGE

            AWSConfigs.AWSProfilesLocation = $"{Environment.GetEnvironmentVariable("UserProfile")}\\.aws\\credentials";
            Environment.SetEnvironmentVariable("BUCKET", $"{Environment.UserName}-pricelist");
            Environment.SetEnvironmentVariable("PRICELIST_FORMAT", "json");
          
            TestLambdaLogger TestLogger = new TestLambdaLogger();
            TestClientContext ClientContext = new TestClientContext();

            TestLambdaContext Context = new TestLambdaContext()
            {
                FunctionName = "PriceListApiFormatter",
                FunctionVersion = "1",
                Logger = TestLogger,
                ClientContext = ClientContext
            };

            ServiceRequest SR = new ServiceRequest("AmazonEC2");

            Entrypoint Ep = new Entrypoint();

            // ACT
            await Ep.Exec(SR, Context);

            // ASSERT
        }
    }
}
