using Amazon.Lambda;
using Amazon.Lambda.TestUtilities;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.SimpleNotificationService;
using BAMCIS.LambdaFunctions.AWSPriceListReservedInstanceHelper;
using BAMCIS.LambdaFunctions.AWSPriceListReservedInstanceHelper.Models;
using Moq;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace AWSPriceListReservedInstanceHelper.Tests
{
    public class FunctionTest
    {
        public FunctionTest()
        {
            Environment.SetEnvironmentVariable("BUCKET", "mybucket");
        }

        [Fact]
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

        [Fact]
        public async Task TestEntrypointJsonEC2()
        {
            // ARRANGE
            Environment.SetEnvironmentVariable("PRICELIST_FORMAT", "json");

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
            Environment.SetEnvironmentVariable("PRICELIST_FORMAT", "json");
            Environment.SetEnvironmentVariable("BUCKET", "mybucket");

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
            Environment.SetEnvironmentVariable("PRICELIST_FORMAT", "json");

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
            Environment.SetEnvironmentVariable("PRICELIST_FORMAT", "json");

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
            Environment.SetEnvironmentVariable("PRICELIST_FORMAT", "json");

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
            Environment.SetEnvironmentVariable("PRICELIST_FORMAT", "json");

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
