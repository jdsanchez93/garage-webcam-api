using Amazon.S3;
using Amazon.S3.Model;
using Amazon.SQS;
using Amazon.SQS.Model;
using garage_webcam_api.Entities;
using Microsoft.AspNetCore.Mvc;

namespace garage_webcam_api.Controllers;

[ApiController]
[Route("[controller]")]
public class WebcamController : ControllerBase
{
    private readonly ILogger<WebcamController> _logger;
    private readonly IConfiguration _configuration;

    public const int MAX_RETRIES = 5;

    public WebcamController(ILogger<WebcamController> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var queueName = _configuration["Aws:QueueUrl"];
        var bucketName = _configuration["Aws:BucketName"];
        var sqsClient = new AmazonSQSClient();

        var imageId = Guid.NewGuid().ToString();
        await SendMessage(sqsClient, queueName, imageId);

        var s3Client = new AmazonS3Client();

        var s3key = imageId + ".png";


        var b = await DoesPrefixExist(s3Client, bucketName, s3key);
        Console.WriteLine($"DoesPrefixExist {b}");

        var presignedUrl = GeneratePreSignedURL(bucketName, s3key, s3Client, 1);

        var ret = new GarageImage()
        {
            Key = imageId,
            Url = presignedUrl
        };
        return Ok(ret);
    }

    //
    // Method to put a message on a queue
    // Could be expanded to include message attributes, etc., in a SendMessageRequest
    private static async Task SendMessage(IAmazonSQS sqsClient, string qUrl, string messageBody)
    {
        SendMessageResponse responseSendMsg = await sqsClient.SendMessageAsync(qUrl, messageBody);
    }

    private static string GeneratePreSignedURL(string bucketName, string objectKey, AmazonS3Client s3Client, double duration)
    {
        string urlString = "";
        try
        {
            GetPreSignedUrlRequest request1 = new GetPreSignedUrlRequest
            {
                BucketName = bucketName,
                Key = objectKey,
                Expires = DateTime.UtcNow.AddHours(duration)
            };
            urlString = s3Client.GetPreSignedURL(request1);
        }
        catch (AmazonS3Exception e)
        {
            Console.WriteLine("Error encountered on server. Message:'{0}' when writing an object", e.Message);
        }
        catch (Exception e)
        {
            Console.WriteLine("Unknown encountered on server. Message:'{0}' when writing an object", e.Message);
        }
        return urlString;
    }

    private async Task<Boolean> DoesPrefixExist(AmazonS3Client s3Client, string bucketName, string prefix)
    {
        try
        {
            ListObjectsV2Request request = new ListObjectsV2Request
            {
                BucketName = bucketName,
                Prefix = prefix
            };

            var retries = 0;
            var retry = true;

            do
            {
                var delay = Math.Pow(2, retries) * 100;
                Console.WriteLine($"delay {delay}");
                await Task.Delay(((int)delay));

                ListObjectsV2Response response = await s3Client.ListObjectsV2Async(request);
                Console.WriteLine($"ListObjectsV2Async {response.HttpStatusCode}");
                Console.WriteLine($"Objects.count {response.S3Objects.Count()}");
                foreach (var item in response.S3Objects)
                {
                    Console.WriteLine(item.Key);
                }

                if (response.S3Objects.Count() >= 1)
                {
                    retry = false;
                }
                retries += 1;


            } while (retry && (retries < MAX_RETRIES));

            return retry;

        }
        catch (System.Exception e)
        {
            this._logger.LogError(e, "Error calling ListObjectsV2Async");
            return false;
        }
    }
}
