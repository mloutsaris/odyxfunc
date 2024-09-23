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
            string fullPath_Odyx = Path.Combine("Files", "OdyxLogo_Face.png");

            try
            {
                // Initialize the BlobServiceClient
                BlobServiceClient blobServiceClient = new BlobServiceClient(_connectionString);
                BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(_contractPdfsContainerName);
                BlobClient blobClient = containerClient.GetBlobClient(blobName);

                // Download the blob content
                BlobDownloadInfo download = await blobClient.DownloadAsync();

                // Save the downloaded PDF locally
                string tempFilePath = Path.GetTempFileName();
                using (var fileStream = File.OpenWrite(tempFilePath))
                {
                    await download.Content.CopyToAsync(fileStream);
                }

                // Load the PDF for modification
                using (PdfDocumentProcessor pdfDocumentProcessor = new PdfDocumentProcessor())
                {
                    pdfDocumentProcessor.LoadDocument(tempFilePath);

                    // Add the logo to the first page (index 0), customize the coordinates as necessary
                    AddImageToPdfPage(pdfDocumentProcessor, 0, fullPath_Odyx, 50, 50, 100, 100);

                    // Save the updated PDF
                    string updatedPdfPath = Path.GetTempFileName();
                    pdfDocumentProcessor.SaveDocument(updatedPdfPath);

                    // Upload the modified PDF back to Blob Storage
                    BlobClient uploadBlobClient = containerClient.GetBlobClient($"modified-{blobName}");

                    using (FileStream uploadFileStream = File.OpenRead(updatedPdfPath))
                    {
                        await uploadBlobClient.UploadAsync(uploadFileStream, true);
                    }
                }

                return new OkObjectResult($"PDF has been updated and re-uploaded as modified-{blobName}");
            }
            catch (Exception ex)
            {
                // Log the error (optional) and return a Not Found or error response
                return new BadRequestObjectResult($"Error: {ex.Message}");
            }
        }

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
