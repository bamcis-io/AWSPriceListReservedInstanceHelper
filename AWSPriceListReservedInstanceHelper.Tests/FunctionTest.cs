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
            // Environment.SetEnvironmentVariable("BUCKET", $"{Environment.UserName}-pricelist");
            // AWSConfigs.AWSProfilesLocation = $"{Environment.GetEnvironmentVariable("UserProfile")}\\.aws\\credentials";
        }

        [Fact]
        public async Task TestEntrypointCsv()
        {
            // ARRANGE
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
            await Ep.RunForServiceAsync(SR, Context);

            // ASSERT
        }

        [Fact]
        public async Task TestEntrypointJson()
        {
            // ARRANGE
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
            await Ep.RunForServiceAsync(SR, Context);

            // ASSERT
        }

        [Fact]
        public async Task TestEntrypointJsonRedshift()
        {
            // ARRANGE
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

            ServiceRequest SR = new ServiceRequest("AmazonRedshift");

            Entrypoint Ep = new Entrypoint();

            // ACT
            await Ep.RunForServiceAsync(SR, Context);

            // ASSERT
        }

        [Fact]
        public async Task TestEntrypointJsonDynamoDB()
        {
            // ARRANGE
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

            ServiceRequest SR = new ServiceRequest("AmazonDynamoDB");

            Entrypoint Ep = new Entrypoint();

            // ACT
            await Ep.RunForServiceAsync(SR, Context);

            // ASSERT
        }

        [Fact]
        public async Task TestEntrypointJsonElasticsearch()
        {
            // ARRANGE
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

            ServiceRequest SR = new ServiceRequest("AmazonES");

            Entrypoint Ep = new Entrypoint();

            // ACT
            await Ep.RunForServiceAsync(SR, Context);

            // ASSERT
        }

        [Fact]
        public async Task TestEntrypointCsvRedshift()
        {
            // ARRANGE
            TestLambdaLogger TestLogger = new TestLambdaLogger();
            TestClientContext ClientContext = new TestClientContext();

            TestLambdaContext Context = new TestLambdaContext()
            {
                FunctionName = "PriceListApiFormatter",
                FunctionVersion = "1",
                Logger = TestLogger,
                ClientContext = ClientContext
            };

            ServiceRequest SR = new ServiceRequest("AmazonRedshift");

            Entrypoint Ep = new Entrypoint();

            // ACT
            await Ep.RunForServiceAsync(SR, Context);

            // ASSERT
        }

        [Fact]
        public async Task TestEntrypointCsvRDS()
        {
            // ARRANGE
            TestLambdaLogger testLogger = new TestLambdaLogger();
            TestClientContext clientContext = new TestClientContext();

            TestLambdaContext Context = new TestLambdaContext()
            {
                FunctionName = "PriceListApiFormatter",
                FunctionVersion = "1",
                Logger = testLogger,
                ClientContext = clientContext
            };

            ServiceRequest request = new ServiceRequest("AmazonRDS");

            Entrypoint entrypoint = new Entrypoint();

            // ACT
            await entrypoint.RunForServiceAsync(request, Context);

            // ASSERT
        }

        [Fact]
        public async Task TestEntrypointCsvDynamoDB()
        {
            // ARRANGE
            TestLambdaLogger TestLogger = new TestLambdaLogger();
            TestClientContext ClientContext = new TestClientContext();

            TestLambdaContext Context = new TestLambdaContext()
            {
                FunctionName = "PriceListApiFormatter",
                FunctionVersion = "1",
                Logger = TestLogger,
                ClientContext = ClientContext
            };

            ServiceRequest SR = new ServiceRequest("AmazonDynamoDB");

            Entrypoint Ep = new Entrypoint();

            // ACT
            await Ep.RunForServiceAsync(SR, Context);

            // ASSERT
        }

        [Fact]
        public async Task TestEntrypointCsvElasticsearch()
        {
            // ARRANGE
            TestLambdaLogger TestLogger = new TestLambdaLogger();
            TestClientContext ClientContext = new TestClientContext();

            TestLambdaContext Context = new TestLambdaContext()
            {
                FunctionName = "PriceListApiFormatter",
                FunctionVersion = "1",
                Logger = TestLogger,
                ClientContext = ClientContext
            };

            ServiceRequest SR = new ServiceRequest("AmazonES");

            Entrypoint Ep = new Entrypoint();

            // ACT
            await Ep.RunForServiceAsync(SR, Context);

            // ASSERT
        }
    }
}
