using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using static System.Net.Mime.MediaTypeNames;
using System.Drawing;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using DevExpress.Pdf;
using Newtonsoft.Json;

namespace odyxfunc
{
    public class Function1
    {
        private readonly ILogger<Function1> _logger;
        private readonly string _connectionString;
        private readonly string _contractPdfsContainerName;


        public Function1(ILogger<Function1> logger)
        {
            _logger = logger;
            _contractPdfsContainerName = Environment.GetEnvironmentVariable("CONTRACTPDFS_CONTAINER_NAME");
            _connectionString = Environment.GetEnvironmentVariable("AZURESTORAGE_CONNECTION_STRING");

        }

        [Function("test")]
        public IActionResult test2([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequest req)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");
            return new OkObjectResult("Welcome to Azure Functions!");
        }

        [Function("AddLogoToPDF")]
        public async Task<IActionResult> getblob([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequest req)
        {
            string blobName;

            // Extract the blobName from the query parameters (for GET requests)
            if (req.Method == HttpMethods.Get)
            {
                blobName = req.Query["blobName"];
            }
            // Extract the blobName from the request body (for POST requests)
            else if (req.Method == HttpMethods.Post)
            {
                // Read the request body
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                dynamic data = JsonConvert.DeserializeObject(requestBody);
                blobName = data?.blobName;
            }
            else
            {
                return new BadRequestObjectResult("Unsupported HTTP method");
            }

            // Validate that blobName is not null or empty
            if (string.IsNullOrEmpty(blobName))
            {
                return new BadRequestObjectResult("blobName is required.");
            }

            //used site base64-image.de
            string fulllogo_base64 = "data:image/png;base64,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";
            string faceonly_base64 = "data:image/png;base64,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";
            
            try
            {

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
                    for (int pageIndex = 0; pageIndex < pdfDocumentProcessor.Document.Pages.Count; pageIndex++)
                    {
                        // Add the logo to the first page (index 0), customize the coordinates as necessary
                        AddBase64ImageToPdfPage(pdfDocumentProcessor, pageIndex, fulllogo_base64, 50, 27, 144, 40,true);
                        AddBase64ImageToPdfPage(pdfDocumentProcessor, pageIndex, faceonly_base64, 50, 1075, 40, 40,true);
                    }

                    // Save the updated PDF
                    string updatedPdfPath = Path.GetTempFileName();
                    pdfDocumentProcessor.SaveDocument(updatedPdfPath);

                    // Upload the modified PDF back to Blob Storage
                    BlobClient uploadBlobClient = containerClient.GetBlobClient(blobName);

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

        private void AddBase64ImageToPdfPage(PdfDocumentProcessor pdfDocumentProcessor, int pageIndex, string base64Image, float x, float y, float width, float height,bool cover = false)
        {
            // Strip the Base64 header (data:image/png;base64,) if it's present
            string base64String = base64Image.Contains(",") ? base64Image.Split(',')[1] : base64Image;

            // Decode Base64 string into a byte array
            byte[] imageBytes = Convert.FromBase64String(base64String);

            // Convert byte array into an image
            using (MemoryStream ms = new MemoryStream(imageBytes))
            {
                using (System.Drawing.Image image = System.Drawing.Image.FromStream(ms))
                {
                    // Create a graphics context for drawing images
                    using (DevExpress.Pdf.PdfGraphics graphics = pdfDocumentProcessor.CreateGraphics())
                    {
                        // Define the rectangle where the image will be placed
                        System.Drawing.RectangleF imageRect = new System.Drawing.RectangleF(x, y, width, height);

                        if (cover)
                        {
                            // Draw a white rectangle as the background
                            using (System.Drawing.Brush whiteBrush = new System.Drawing.SolidBrush(System.Drawing.Color.White))
                            {
                                graphics.FillRectangle(whiteBrush, imageRect);
                            }
                        }

                        // Draw the image on top of the white background
                        graphics.DrawImage(image, imageRect);

                        // Finalize the drawing operation by adding it to the page
                        graphics.AddToPageForeground(pdfDocumentProcessor.Document.Pages[pageIndex]);
                    }
                }
            }
        }
    }
}
