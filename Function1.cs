using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using static System.Net.Mime.MediaTypeNames;
using System.Drawing;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using DevExpress.Pdf;

namespace odyxfunc
{
    public class Function1
    {
        private readonly ILogger<Function1> _logger;
        private readonly string _contractsContainerName;
        private readonly string _connectionString;
        private readonly string _contractPdfsContainerName;


        public Function1(ILogger<Function1> logger)
        {
            _logger = logger;
            _contractPdfsContainerName = Environment.GetEnvironmentVariable("CONTRACTPDFS_CONTAINER_NAME");
            _connectionString = Environment.GetEnvironmentVariable("AZURESTORAGE_CONNECTION_STRING");

        }

        [Function("test2")]
        public IActionResult test2([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequest req)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");
            return new OkObjectResult("Welcome to Azure Functions!");
        }

        [Function("getblob")]
        public async Task<IActionResult> getblob([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequest req)
        {
            string blobName = "82b11634-17fe-4137-b4a5-1ff0fb09aac5.pdf";

            try
            {
                // Initialize the BlobServiceClient
                BlobServiceClient blobServiceClient = new BlobServiceClient(_connectionString);
                BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(_contractPdfsContainerName);
                BlobClient blobClient = containerClient.GetBlobClient(blobName);

                // Download the blob content
                BlobDownloadInfo download = await blobClient.DownloadAsync();

                // Convert blob to string or return file content
                using (StreamReader reader = new StreamReader(download.Content))
                {
                    string fileContent = await reader.ReadToEndAsync();
                    return new OkObjectResult(fileContent); // Return the file content as string with 200 OK status
                }
            }
            catch (Exception ex)
            {
                // Log the error (optional) and return a Not Found or error response
                return new BadRequestObjectResult($"Error: {ex.Message}");
            }
        }

        [Function("AddImageToPdfPage")]
        private void AddImageToPdfPage(PdfDocumentProcessor pdfDocumentProcessor, int pageIndex, string imagePath, float x, float y, float width, float height)
        {
            // Create a graphics context for drawing images
            using (DevExpress.Pdf.PdfGraphics graphics = pdfDocumentProcessor.CreateGraphics())
            {
                // Load the image from file
                using (System.Drawing.Image image = System.Drawing.Image.FromFile(imagePath))
                {
                    // Define the rectangle where the image will be placed
                    System.Drawing.RectangleF imageRect = new RectangleF(x, y, width, height);

                    // Draw the image on the specified page at the given coordinates
                    graphics.DrawImage(image, imageRect);

                    // Finalize the drawing operation by adding it to the page
                    graphics.AddToPageForeground(pdfDocumentProcessor.Document.Pages[pageIndex]);
                }
            }
        }
    }
}
