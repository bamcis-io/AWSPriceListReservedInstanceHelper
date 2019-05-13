# BAMCIS AWS Price List Reserved Instance Helper

This application parses the price list API content for Amazon EC2, RDS, and ElastiCache. It retrieves the pricing terms
that are relevant to Reserved Instance purchases for each of those services and stores them in S3 so that they can be 
queried through tools like Athena or EMR.

## Table of Contents
- [Usage](#usage)
- [Notifications](#notifications)
- [Revision History](#revision-history)

## Usage

The application consists of 2 functions. The first function is triggered by the scheduled event. It then launches 3 separate functions, one 
for each service to ensure there are no timeout issues when trying to parse the price list files.

Deploy the application into any account specifying the frequency you want it to run. The function will setup the S3 bucket
as well where the results will be stored. For each SKU, i.e. a unique reservable item type (a combination of instance type, platform, region,
and tenancy), there will be line items for all of the reserved instance purchase options for that unique item type. For example, you'll have
a row for 1 year all upfront standard and a row for 3 year no upfront convertible, etc. 

Here's some example data from a simple Athena query:

| sku              | offertermcode | platform | tenancy | operation         | usagetype               | region    | service   | instancetype | operatingsystem | adjustedpriceperunit | ondemandhourlycost | breakevenpercentage | upfrontfee | leaseterm | purchaseoption  | offeringclass | termtype | key                             | reservedinstancecost | ondemandcostforterm | costsavings | percentsavings | vcpu | memory |
|------------------|---------------|----------|---------|-------------------|-------------------------|-----------|-----------|--------------|-----------------|----------------------|--------------------|---------------------|------------|-----------|-----------------|---------------|----------|---------------------------------|----------------------|---------------------|-------------|----------------|------|--------|
| 223BX6UNNB3JE9ET | BPH4J8HBKS    | SUSE     | Shared  | RunInstances:000g | USW2-BoxUsage:r4.xlarge | us-west-2 | AmazonEC2 | r4.xlarge    | SUSE            | 0.1475               | 0.366              | 0.403005464         | 0          | 3         | NO_UPFRONT      | STANDARD      | RESERVED | 3::NO_UPFRONT::STANDARD         | 3876.3               | 9618.48             | 5742.18     | 59.699         | 4    | 30.5   |
| 223BX6UNNB3JE9ET | NQ3QZPMQV9    | SUSE     | Shared  | RunInstances:000g | USW2-BoxUsage:r4.xlarge | us-west-2 | AmazonEC2 | r4.xlarge    | SUSE            | 0                    | 0.366              | 0.345376816         | 3322       | 3         | ALL_UPFRONT     | STANDARD      | RESERVED | 3::ALL_UPFRONT::STANDARD        | 3322                 | 9618.48             | 6296.48     | 65.462         | 4    | 30.5   |
| 223BX6UNNB3JE9ET | 6QCMYABX3D    | SUSE     | Shared  | RunInstances:000g | USW2-BoxUsage:r4.xlarge | us-west-2 | AmazonEC2 | r4.xlarge    | SUSE            | 0                    | 0.366              | 0.517753325         | 1660       | 1         | ALL_UPFRONT     | STANDARD      | RESERVED | 1::ALL_UPFRONT::STANDARD        | 1660                 | 3206.16             | 1546.16     | 48.225         | 4    | 30.5   |
| 223BX6UNNB3JE9ET | R5XV2EPZQZ    | SUSE     | Shared  | RunInstances:000g | USW2-BoxUsage:r4.xlarge | us-west-2 | AmazonEC2 | r4.xlarge    | SUSE            | 0.0739               | 0.366              | 0.40506317          | 1954       | 3         | PARTIAL_UPFRONT | CONVERTIBLE   | RESERVED | 3::PARTIAL_UPFRONT::CONVERTIBLE | 3896.092             | 9618.48             | 5722.388    | 59.494         | 4    | 30.5   |
| 223BX6UNNB3JE9ET | 7NE97W5U4E    | SUSE     | Shared  | RunInstances:000g | USW2-BoxUsage:r4.xlarge | us-west-2 | AmazonEC2 | r4.xlarge    | SUSE            | 0.2262               | 0.366              | 0.618032787         | 0          | 1         | NO_UPFRONT      | CONVERTIBLE   | RESERVED | 1::NO_UPFRONT::CONVERTIBLE      | 1981.512             | 3206.16             | 1224.648    | 38.197         | 4    | 30.5   |
| 223BX6UNNB3JE9ET | MZU6U2429S    | SUSE     | Shared  | RunInstances:000g | USW2-BoxUsage:r4.xlarge | us-west-2 | AmazonEC2 | r4.xlarge    | SUSE            | 0                    | 0.366              | 0.398503714         | 3833       | 3         | ALL_UPFRONT     | CONVERTIBLE   | RESERVED | 3::ALL_UPFRONT::CONVERTIBLE     | 3833                 | 9618.48             | 5785.48     | 60.15          | 4    | 30.5   |
| 223BX6UNNB3JE9ET | 4NA7Y494T4    | SUSE     | Shared  | RunInstances:000g | USW2-BoxUsage:r4.xlarge | us-west-2 | AmazonEC2 | r4.xlarge    | SUSE            | 0.201                | 0.366              | 0.549180328         | 0          | 1         | NO_UPFRONT      | STANDARD      | RESERVED | 1::NO_UPFRONT::STANDARD         | 1760.76              | 3206.16             | 1445.4      | 45.082         | 4    | 30.5   |
| 223BX6UNNB3JE9ET | HU7G6KETJZ    | SUSE     | Shared  | RunInstances:000g | USW2-BoxUsage:r4.xlarge | us-west-2 | AmazonEC2 | r4.xlarge    | SUSE            | 0.093                | 0.366              | 0.527010505         | 875        | 1         | PARTIAL_UPFRONT | STANDARD      | RESERVED | 1::PARTIAL_UPFRONT::STANDARD    | 1689.68              | 3206.16             | 1516.48     | 47.299         | 4    | 30.5   |
| 223BX6UNNB3JE9ET | VJWZNREJX2    | SUSE     | Shared  | RunInstances:000g | USW2-BoxUsage:r4.xlarge | us-west-2 | AmazonEC2 | r4.xlarge    | SUSE            | 0                    | 0.366              | 0.583252239         | 1870       | 1         | ALL_UPFRONT     | CONVERTIBLE   | RESERVED | 1::ALL_UPFRONT::CONVERTIBLE     | 1870                 | 3206.16             | 1336.16     | 41.675         | 4    | 30.5   |
| 223BX6UNNB3JE9ET | CUZHX8X6JH    | SUSE     | Shared  | RunInstances:000g | USW2-BoxUsage:r4.xlarge | us-west-2 | AmazonEC2 | r4.xlarge    | SUSE            | 0.105                | 0.366              | 0.593170647         | 982        | 1         | PARTIAL_UPFRONT | CONVERTIBLE   | RESERVED | 1::PARTIAL_UPFRONT::CONVERTIBLE | 1901.8               | 3206.16             | 1304.36     | 40.683         | 4    | 30.5   |
| 223BX6UNNB3JE9ET | 38NPMPTW36    | SUSE     | Shared  | RunInstances:000g | USW2-BoxUsage:r4.xlarge | us-west-2 | AmazonEC2 | r4.xlarge    | SUSE            | 0.066                | 0.366              | 0.362269298         | 1750       | 3         | PARTIAL_UPFRONT | STANDARD      | RESERVED | 3::PARTIAL_UPFRONT::STANDARD    | 3484.48              | 9618.48             | 6134        | 63.773         | 4    | 30.5   |
| 223BX6UNNB3JE9ET | Z2E3P23VKM    | SUSE     | Shared  | RunInstances:000g | USW2-BoxUsage:r4.xlarge | us-west-2 | AmazonEC2 | r4.xlarge    | SUSE            | 0.1647               | 0.366              | 0.45                | 0          | 3         | NO_UPFRONT      | CONVERTIBLE   | RESERVED | 3::NO_UPFRONT::CONVERTIBLE      | 4328.316             | 9618.48             | 5290.164    | 55             | 4    | 30.5   |

Additional data has been added to each row to easily identify the breakeven utilization percentage where purchasing the RI is more cost
effective than paying the on demand costs as well as the percent savings and the standard on-demand hourly cost. This gives a robust way
to quickly view and analyze current RI pricing as well as combine this data with actual usage data from AWS Cost and Usage Reports (aka billing
files) to generate Reserved Instance purchase recommendations.

The delimiter used in the results is the `|` symbol to help alleviate issues with commas being used in columns. You can specify
a different delimiter by providing an environment variable, `DELIMITER`, to the function. The results bucket is also provided to the function
as an environment variable, `BUCKET`. The secondary function is identified to the first function as an environment variable `FunctionName`. Lastly,
this format of the price list data that the second lambda function pulls down is specified by the `PRICELIST_FORMAT` environment variable. If it's not
set or doesn't match an expected value, `csv` is used.

## Notifications
Both Lambda functions accept an environment variable `SNS` that is an ARN to an SNS topic. When an exception is encountered during processing that
would normally end execution, an SNS notification can be sent to the specified topic. All other exceptions, warnings, and info are logged to CloudWatch
Logs. You could also pair these functions with a function that sent an SNS message on a `PutObject` S3 action to notify you that new RI pricing data
files have been delivered.

Additional monitoring has been added in version 1.3.0. CloudWatch Alarms now monitor failed Lambda function invocations and deliver SNS notifications when they occur. A separate CloudWatch Alarm monitors for at least 1 invocation of the Distributor function and 5 invocations of the Worker function to ensure the invocations are actually occuring. If you have the frequency scheduled for more than 1 day, expect to see SNS messages concerning the invocation frequency. Lastly, Lambda Dead Letter Queues (DLQ) have been added for each function (where the request to the function gets sent after 3 failed execution attempts). When the DLQ exceeds 0 messages, a CloudWatch Alarm is triggered and sends an SNS notification. It is up to the user to decide what to do with the messages in the DLQ.

## Revision History

### 2.1.0
Removed the frequency parameters and replaced the scheduled event trigger with SNS notification triggers that AWS provides when the price list is updated. Fixed error handling when building reserved instance pricing terms.

### 2.0.0
Added support for deployment through Codepipeline.

### 1.3.0
Added enhanced monitoring.

### 1.2.2
Switched the flush order for the stream writers and added awaits.

### 1.2.1
Code cleanup.

### 1.2.0
Updated how both csv and json files are processed and filtered.

### 1.1.1
Fixed Redhsift and DynamoDB formatting.

### 1.1.0
Added support for Redshift and DynamoDB.

### 1.0.1
Updated CF template default parameter values. Updated to .NET Core 2.1.

### 1.0.0
Initial release of the application.
