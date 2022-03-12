using Amazon.S3;
using Amazon.S3.Model;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.AspNetCore.Mvc;

namespace garage_webcam_api.Controllers;

[ApiController]
[Route("[controller]")]
public class WebcamController : ControllerBase
{
    private readonly ILogger<WebcamController> _logger;
    private readonly IConfiguration _configuration;

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

        var message = Guid.NewGuid().ToString();
        await SendMessage(sqsClient, queueName, message);

        var s3Client = new AmazonS3Client();
        var presignedUrl = GeneratePreSignedURL(bucketName, message + ".png", s3Client, 1);

        return Ok(presignedUrl);
    }

    //
    // Method to put a message on a queue
    // Could be expanded to include message attributes, etc., in a SendMessageRequest
    private static async Task SendMessage(IAmazonSQS sqsClient, string qUrl, string messageBody)
    {
        SendMessageResponse responseSendMsg = await sqsClient.SendMessageAsync(qUrl, messageBody);
        Console.WriteLine($"Message added to queue\n  {qUrl}");
        Console.WriteLine($"HttpStatusCode: {responseSendMsg.HttpStatusCode}");
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
}
