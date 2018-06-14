using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Xunit;
using Amazon.Lambda.Core;
using Amazon.Lambda.TestUtilities;
using Amazon.Lambda.APIGatewayEvents;

using PriceListApiFormatter;
using BAMCIS.LambdaFunctions.PriceListApiFormatter;
using BAMCIS.AWSLambda.Common.Events;
using Amazon.Runtime;
using Amazon.Runtime.CredentialManagement;
using System.Diagnostics;

namespace PriceListApiFormatter.Tests
{
    public class FunctionTest
    {
        public FunctionTest()
        {
        }

        [Fact]
        public async Task TestEntrypoint()
        {
            // ARRANGE
            
            CloudWatchScheduledEvent Event = new CloudWatchScheduledEvent(
                "0",
                Guid.Parse("125e7841-c049-462d-86c2-4efa5f64e293"),
                "123456789012",
                DateTime.Parse("2016-12-16T19:55:42Z"),
                "us-east-1",
                new string[] { "arn:aws:events:us-east-1:415720405880:rule/PriceListApiFormatter-TestUrls-X2YM3334N4JN" },
                new object()
            );

            TestLambdaLogger TestLogger = new TestLambdaLogger();
            TestClientContext ClientContext = new TestClientContext();

            SharedCredentialsFile Creds = new SharedCredentialsFile();
            Creds.TryGetProfile("mhaken-dev", out CredentialProfile Profile);

            ImmutableCredentials Cr = Profile.GetAWSCredentials(Creds).GetCredentials();

            ClientContext.Environment.Add("BUCKET", "mhaken-billing");

            TestLambdaContext Context = new TestLambdaContext()
            {
                FunctionName = "PriceListApiFormatter",
                FunctionVersion = "1",
                Logger = TestLogger,
                ClientContext = ClientContext
            };

            Entrypoint Ep = new Entrypoint();

            // ACT
            await Ep.Exec(Event, Context);

            // ASSERT
        }
    }
}
