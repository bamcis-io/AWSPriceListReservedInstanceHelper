using Amazon.Lambda;
using Amazon.Lambda.Model;
using Amazon.Lambda.SNSEvents;
using Amazon.Lambda.TestUtilities;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.SimpleNotificationService;
using BAMCIS.LambdaFunctions.AWSPriceListReservedInstanceHelper;
using BAMCIS.LambdaFunctions.AWSPriceListReservedInstanceHelper.Models;
using Moq;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace AWSPriceListReservedInstanceHelper.Tests
{
    public class FunctionTest
    {
        public FunctionTest()
        {
            System.Environment.SetEnvironmentVariable("BUCKET", "mybucket");
        }

        [Fact]
        public void TestStreamIndexOfSuccessMiddle()
        {
            // ARRANGE
            byte[] bytesToSearch = new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05};

            byte[] pattern = new byte[] { 0x02, 0x03 };

            long index;

            // ACT
            using (MemoryStream stream = new MemoryStream(bytesToSearch, false))
            {
                index = stream.IndexOf(pattern);

                // ASSERT
                Assert.Equal(2, index);
                Assert.Equal(0, stream.Position);
            }          
        }

        [Fact]
        public void TestStreamIndexOfSuccessBeginning()
        {
            // ARRANGE
            byte[] bytesToSearch = new byte[] { 0x02, 0x03, 0x02, 0x03, 0x04, 0x05 };

            byte[] pattern = new byte[] { 0x02, 0x03 };

            long index;

            // ACT
            using (MemoryStream stream = new MemoryStream(bytesToSearch, false))
            {
                index = stream.IndexOf(pattern);

                // ASSERT
                Assert.Equal(0, index);
                Assert.Equal(0, stream.Position);
            }
        }

        [Fact]
        public void TestStreamIndexOfSuccessEnd()
        {
            // ARRANGE
            byte[] bytesToSearch = new byte[] { 0x01, 0x03, 0x02, 0x04, 0x02, 0x03 };

            byte[] pattern = new byte[] { 0x02, 0x03 };

            long index;

            // ACT
            using (MemoryStream stream = new MemoryStream(bytesToSearch, false))
            {
                index = stream.IndexOf(pattern);

                // ASSERT
                Assert.Equal(4, index);
                Assert.Equal(0, stream.Position);
            }
        }

        [Fact]
        public void TestStreamIndexOfFailure()
        {
            // ARRANGE
            byte[] bytesToSearch = new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05 };

            byte[] pattern = new byte[] { 0x03, 0x02 };

            long index;

            // ACT
            using (MemoryStream stream = new MemoryStream(bytesToSearch, false))
            {
                index = stream.IndexOf(pattern);

                // ASSERT
                Assert.Equal(-1, index);
                Assert.Equal(0, stream.Position);
            }
        }

        [Fact]
        public void TestStreamIndexOfFailureNoMatches()
        {
            // ARRANGE
            byte[] bytesToSearch = new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05 };

            byte[] pattern = new byte[] { 0x06, 0x07 };

            long index;

            // ACT
            using (MemoryStream stream = new MemoryStream(bytesToSearch, false))
            {
                index = stream.IndexOf(pattern);

                // ASSERT
                Assert.Equal(-1, index);
                Assert.Equal(0, stream.Position);
            }
        }

        [Fact]
        public void TestStreamIndexOfFailureSomeMatches()
        {
            // ARRANGE
            byte[] bytesToSearch = new byte[] { 0x00, 0x01, 0x02, 0x02, 0x01, 0x03 };

            byte[] pattern = new byte[] { 0x02, 0x03 };

            long index;

            // ACT
            using (MemoryStream stream = new MemoryStream(bytesToSearch, false))
            {
                index = stream.IndexOf(pattern);

                // ASSERT
                Assert.Equal(-1, index);
                Assert.Equal(0, stream.Position);
            }
        }
        
        [Fact]
        public async Task TestDistributor()
        {
            // ARRANGE
            TestLambdaLogger testLogger = new TestLambdaLogger();
            TestClientContext clientContext = new TestClientContext();

            TestLambdaContext context = new TestLambdaContext()
            {
                FunctionName = "PriceListApiFormatter",
                FunctionVersion = "1",
                Logger = testLogger,
                ClientContext = clientContext
            };

            SNSEvent ev = new SNSEvent();

            Mock<IAmazonS3> s3 = new Mock<IAmazonS3>();
            Mock<IAmazonLambda> lambda = new Mock<IAmazonLambda>();
            Mock<IAmazonSimpleNotificationService> sns = new Mock<IAmazonSimpleNotificationService>();            
            lambda.Setup(x => x.InvokeAsync(It.IsAny<InvokeRequest>(), default(CancellationToken))).Returns(Task.FromResult(new InvokeResponse()));

            Entrypoint entry = new Entrypoint(sns.Object, s3.Object, lambda.Object);

            // ACT
            await entry.LaunchWorkersAsync(ev, context);

            // ASSERT
        }

        [Fact (Skip = "Stream is too large to process in dependent library")]
        public async Task TestEntrypointCsvEC2()
        {
            // ARRANGE
            TestLambdaLogger testLogger = new TestLambdaLogger();
            TestClientContext clientContext = new TestClientContext();

            TestLambdaContext context = new TestLambdaContext()
            {
                FunctionName = "PriceListApiFormatter",
                FunctionVersion = "1",
                Logger = testLogger,
                ClientContext = clientContext
            };

            ServiceRequest serviceRequest = new ServiceRequest("AmazonEC2");

            Mock<IAmazonS3> s3 = new Mock<IAmazonS3>();
            Mock<IAmazonLambda> lambda = new Mock<IAmazonLambda>();
            Mock<IAmazonSimpleNotificationService> sns = new Mock<IAmazonSimpleNotificationService>();
            s3.Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), default(CancellationToken))).Returns(Task.FromResult(new PutObjectResponse()));

            Entrypoint entry = new Entrypoint(sns.Object, s3.Object, lambda.Object);

            // ACT
            await entry.RunForServiceAsync(serviceRequest, context);

            // ASSERT
        }

        [Fact (Skip = "Stream is too large to process in dependent library")]
        public async Task TestEntrypointJsonEC2()
        {
            // ARRANGE
            System.Environment.SetEnvironmentVariable("PRICELIST_FORMAT", "json");

            TestLambdaLogger testLogger = new TestLambdaLogger();
            TestClientContext clientContext = new TestClientContext();

            TestLambdaContext context = new TestLambdaContext()
            {
                FunctionName = "PriceListApiFormatter",
                FunctionVersion = "1",
                Logger = testLogger,
                ClientContext = clientContext
            };

            ServiceRequest serviceRequest = new ServiceRequest("AmazonEC2");

            Mock<IAmazonS3> s3 = new Mock<IAmazonS3>();
            Mock<IAmazonLambda> lambda = new Mock<IAmazonLambda>();
            Mock<IAmazonSimpleNotificationService> sns = new Mock<IAmazonSimpleNotificationService>();
            s3.Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), default(CancellationToken))).Returns(Task.FromResult(new PutObjectResponse()));

            Entrypoint entry = new Entrypoint(sns.Object, s3.Object, lambda.Object);


            // ACT
            await entry.RunForServiceAsync(serviceRequest, context);

            // ASSERT
        }

        [Fact]
        public async Task TestEntrypointJsonRedshift()
        {
            // ARRANGE
            System.Environment.SetEnvironmentVariable("PRICELIST_FORMAT", "json");
            System.Environment.SetEnvironmentVariable("BUCKET", "mybucket");

            TestLambdaLogger testLogger = new TestLambdaLogger();
            TestClientContext clientContext = new TestClientContext();

            TestLambdaContext context = new TestLambdaContext()
            {
                FunctionName = "PriceListApiFormatter",
                FunctionVersion = "1",
                Logger = testLogger,
                ClientContext = clientContext
            };

            ServiceRequest serviceRequest = new ServiceRequest("AmazonRedshift");

            Mock<IAmazonS3> s3 = new Mock<IAmazonS3>();
            Mock<IAmazonLambda> lambda = new Mock<IAmazonLambda>();
            Mock<IAmazonSimpleNotificationService> sns = new Mock<IAmazonSimpleNotificationService>();
            s3.Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), default(CancellationToken))).Returns(Task.FromResult(new PutObjectResponse()));

            Entrypoint entry = new Entrypoint(sns.Object, s3.Object, lambda.Object);


            // ACT
            await entry.RunForServiceAsync(serviceRequest, context);

            // ASSERT
        }

        [Fact]
        public async Task TestEntrypointJsonDynamoDB()
        {
            // ARRANGE
            System.Environment.SetEnvironmentVariable("PRICELIST_FORMAT", "json");

            TestLambdaLogger testLogger = new TestLambdaLogger();
            TestClientContext clientContext = new TestClientContext();

            TestLambdaContext context = new TestLambdaContext()
            {
                FunctionName = "PriceListApiFormatter",
                FunctionVersion = "1",
                Logger = testLogger,
                ClientContext = clientContext
            };

            ServiceRequest serviceRequest = new ServiceRequest("AmazonDynamoDB");

            Mock<IAmazonS3> s3 = new Mock<IAmazonS3>();
            Mock<IAmazonLambda> lambda = new Mock<IAmazonLambda>();
            Mock<IAmazonSimpleNotificationService> sns = new Mock<IAmazonSimpleNotificationService>();
            s3.Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), default(CancellationToken))).Returns(Task.FromResult(new PutObjectResponse()));

            Entrypoint entry = new Entrypoint(sns.Object, s3.Object, lambda.Object);


            // ACT
            await entry.RunForServiceAsync(serviceRequest, context);

            // ASSERT
        }

        [Fact]
        public async Task TestEntrypointJsonElasticsearch()
        {
            // ARRANGE
            System.Environment.SetEnvironmentVariable("PRICELIST_FORMAT", "json");

            TestLambdaLogger testLogger = new TestLambdaLogger();
            TestClientContext clientContext = new TestClientContext();

            TestLambdaContext context = new TestLambdaContext()
            {
                FunctionName = "PriceListApiFormatter",
                FunctionVersion = "1",
                Logger = testLogger,
                ClientContext = clientContext
            };

            ServiceRequest serviceRequest = new ServiceRequest("AmazonES");

            Mock<IAmazonS3> s3 = new Mock<IAmazonS3>();
            Mock<IAmazonLambda> lambda = new Mock<IAmazonLambda>();
            Mock<IAmazonSimpleNotificationService> sns = new Mock<IAmazonSimpleNotificationService>();
            s3.Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), default(CancellationToken))).Returns(Task.FromResult(new PutObjectResponse()));

            Entrypoint entry = new Entrypoint(sns.Object, s3.Object, lambda.Object);


            // ACT
            await entry.RunForServiceAsync(serviceRequest, context);

            // ASSERT
        }

        [Fact]
        public async Task TestEntrypointCsvRedshift()
        {
            // ARRANGE
            TestLambdaLogger testLogger = new TestLambdaLogger();
            TestClientContext clientContext = new TestClientContext();

            TestLambdaContext context = new TestLambdaContext()
            {
                FunctionName = "PriceListApiFormatter",
                FunctionVersion = "1",
                Logger = testLogger,
                ClientContext = clientContext
            };

            ServiceRequest serviceRequest = new ServiceRequest("AmazonRedshift");

            Mock<IAmazonS3> s3 = new Mock<IAmazonS3>();
            Mock<IAmazonLambda> lambda = new Mock<IAmazonLambda>();
            Mock<IAmazonSimpleNotificationService> sns = new Mock<IAmazonSimpleNotificationService>();
            s3.Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), default(CancellationToken))).Returns(Task.FromResult(new PutObjectResponse()));

            Entrypoint entry = new Entrypoint(sns.Object, s3.Object, lambda.Object);

            // ACT
            await entry.RunForServiceAsync(serviceRequest, context);

            // ASSERT
        }

        [Fact]
        public async Task TestEntrypointCsvRDS()
        {
            // ARRANGE
            TestLambdaLogger testLogger = new TestLambdaLogger();
            TestClientContext clientContext = new TestClientContext();

            TestLambdaContext context = new TestLambdaContext()
            {
                FunctionName = "PriceListApiFormatter",
                FunctionVersion = "1",
                Logger = testLogger,
                ClientContext = clientContext
            };

            ServiceRequest request = new ServiceRequest("AmazonRDS");

            Mock<IAmazonS3> s3 = new Mock<IAmazonS3>();
            Mock<IAmazonLambda> lambda = new Mock<IAmazonLambda>();
            Mock<IAmazonSimpleNotificationService> sns = new Mock<IAmazonSimpleNotificationService>();
            s3.Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), default(CancellationToken))).Returns(Task.FromResult(new PutObjectResponse()));

            Entrypoint entry = new Entrypoint(sns.Object, s3.Object, lambda.Object);


            // ACT
            await entry.RunForServiceAsync(request, context);

            // ASSERT
        }

        [Fact]
        public async Task TestEntrypointJsonRDS()
        {
            // ARRANGE
            System.Environment.SetEnvironmentVariable("PRICELIST_FORMAT", "json");

            TestLambdaLogger testLogger = new TestLambdaLogger();
            TestClientContext clientContext = new TestClientContext();

            TestLambdaContext context = new TestLambdaContext()
            {
                FunctionName = "PriceListApiFormatter",
                FunctionVersion = "1",
                Logger = testLogger,
                ClientContext = clientContext
            };

            ServiceRequest serviceRequest = new ServiceRequest("AmazonRDS");

            Mock<IAmazonS3> s3 = new Mock<IAmazonS3>();
            Mock<IAmazonLambda> lambda = new Mock<IAmazonLambda>();
            Mock<IAmazonSimpleNotificationService> sns = new Mock<IAmazonSimpleNotificationService>();
            s3.Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), default(CancellationToken))).Returns(Task.FromResult(new PutObjectResponse()));

            Entrypoint entry = new Entrypoint(sns.Object, s3.Object, lambda.Object);


            // ACT
            await entry.RunForServiceAsync(serviceRequest, context);

            // ASSERT
        }

        [Fact]
        public async Task TestEntrypointCsvElastiCache()
        {
            // ARRANGE
            TestLambdaLogger testLogger = new TestLambdaLogger();
            TestClientContext clientContext = new TestClientContext();

            TestLambdaContext context = new TestLambdaContext()
            {
                FunctionName = "PriceListApiFormatter",
                FunctionVersion = "1",
                Logger = testLogger,
                ClientContext = clientContext
            };

            ServiceRequest request = new ServiceRequest("AmazonElastiCache");

            Mock<IAmazonS3> s3 = new Mock<IAmazonS3>();
            Mock<IAmazonLambda> lambda = new Mock<IAmazonLambda>();
            Mock<IAmazonSimpleNotificationService> sns = new Mock<IAmazonSimpleNotificationService>();
            s3.Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), default(CancellationToken))).Returns(Task.FromResult(new PutObjectResponse()));

            Entrypoint entry = new Entrypoint(sns.Object, s3.Object, lambda.Object);


            // ACT
            await entry.RunForServiceAsync(request, context);

            // ASSERT
        }

        [Fact]
        public async Task TestEntrypointJsonElastiCache()
        {
            // ARRANGE
            System.Environment.SetEnvironmentVariable("PRICELIST_FORMAT", "json");

            TestLambdaLogger testLogger = new TestLambdaLogger();
            TestClientContext clientContext = new TestClientContext();

            TestLambdaContext context = new TestLambdaContext()
            {
                FunctionName = "PriceListApiFormatter",
                FunctionVersion = "1",
                Logger = testLogger,
                ClientContext = clientContext
            };

            ServiceRequest serviceRequest = new ServiceRequest("AmazonElastiCache");

            Mock<IAmazonS3> s3 = new Mock<IAmazonS3>();
            Mock<IAmazonLambda> lambda = new Mock<IAmazonLambda>();
            Mock<IAmazonSimpleNotificationService> sns = new Mock<IAmazonSimpleNotificationService>();
            s3.Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), default(CancellationToken))).Returns(Task.FromResult(new PutObjectResponse()));

            Entrypoint entry = new Entrypoint(sns.Object, s3.Object, lambda.Object);


            // ACT
            await entry.RunForServiceAsync(serviceRequest, context);

            // ASSERT
        }

        [Fact]
        public async Task TestEntrypointCsvDynamoDB()
        {
            // ARRANGE
            TestLambdaLogger testLogger = new TestLambdaLogger();
            TestClientContext clientContext = new TestClientContext();

            TestLambdaContext context = new TestLambdaContext()
            {
                FunctionName = "PriceListApiFormatter",
                FunctionVersion = "1",
                Logger = testLogger,
                ClientContext = clientContext
            };

            ServiceRequest serviceRequest = new ServiceRequest("AmazonDynamoDB");

            Mock<IAmazonS3> s3 = new Mock<IAmazonS3>();
            Mock<IAmazonLambda> lambda = new Mock<IAmazonLambda>();
            Mock<IAmazonSimpleNotificationService> sns = new Mock<IAmazonSimpleNotificationService>();
            s3.Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), default(CancellationToken))).Returns(Task.FromResult(new PutObjectResponse()));

            Entrypoint entry = new Entrypoint(sns.Object, s3.Object, lambda.Object);


            // ACT
            await entry.RunForServiceAsync(serviceRequest, context);

            // ASSERT
        }

        [Fact]
        public async Task TestEntrypointCsvElasticsearch()
        {
            // ARRANGE
            TestLambdaLogger testLogger = new TestLambdaLogger();
            TestClientContext clientContext = new TestClientContext();

            TestLambdaContext context = new TestLambdaContext()
            {
                FunctionName = "PriceListApiFormatter",
                FunctionVersion = "1",
                Logger = testLogger,
                ClientContext = clientContext
            };

            ServiceRequest serviceRequest = new ServiceRequest("AmazonES");

            Mock<IAmazonS3> s3 = new Mock<IAmazonS3>();
            Mock<IAmazonLambda> lambda = new Mock<IAmazonLambda>();
            Mock<IAmazonSimpleNotificationService> sns = new Mock<IAmazonSimpleNotificationService>();
            s3.Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), default(CancellationToken))).Returns(Task.FromResult(new PutObjectResponse()));

            Entrypoint entry = new Entrypoint(sns.Object, s3.Object, lambda.Object);


            // ACT
            await entry.RunForServiceAsync(serviceRequest, context);

            // ASSERT
        }
    }
}
