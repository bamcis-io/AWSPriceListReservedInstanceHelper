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
using BAMCIS.AWSPriceListApi.Serde;

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

            ClientContext.Environment.Add("BUCKET", "mhaken-pricelist");

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

        [Fact]
        public void TestEx()
        {
            List<Tuple<string, string>> Data = new List<Tuple<string, string>>()
            {
                new Tuple<string, string>("test1", "test1"),
                new Tuple<string, string>("test1", "test2"),
                new Tuple<string, string>("test1", "test3"),
                new Tuple<string, string>("test3", "test1"),
                new Tuple<string, string>("test3", "test2"),
                new Tuple<string, string>("test3", "test3"),
            };

            IEnumerable<IGrouping<string, Tuple<string, string>>> Input = Data.GroupBy(x => x.Item1);

            try
            {
                IEnumerable<string> Temp = Input.SelectMany(x =>
                {
                    try
                    {
                        return InnerClass.Build();
                    }
                    catch (Exception e)
                    {
                        return new string[0];
                    }
                });

                var T = Temp.ToList();
            }
            catch (Exception e)            
            {
                int j = 0;
            }

            int i = 0;
        }

        internal class InnerClass
        {
            public IEnumerable<string> Prop { get; }

            public static IEnumerable<string> Build()
            {
                throw new KeyNotFoundException();
            }
        }
    }
}
