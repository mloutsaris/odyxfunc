using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using DevExpress.Pdf;
using Newtonsoft.Json;
using DevExpress.Drawing;
using DevExpress.Utils;

namespace odyxfunc
{
    public class Function1
    {
        private readonly ILogger<Function1> _logger;
        private readonly string _connectionString;
        private readonly string _connectionString_DEV;
        private readonly string _contractPdfsContainerName;



        public Function1(ILogger<Function1> logger)
        {
            _logger = logger;
            _contractPdfsContainerName = "";
            _connectionString_DEV = "";
            _connectionString = "";

        }

        public class PdfModificationRequest
        {
            public string PdfBytes { get; set; } // The PDF in base64 format
            public string HeaderLogo { get; set; } // Base64-encoded header logo
            public string FooterLogo { get; set; } // Base64-encoded footer logo
        }

        [Function("test")]
        public IActionResult test2([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequest req)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");
            return new OkObjectResult("Welcome to Azure Functions!");
        }


        [Function("AddLogoToPdfFromBase64ByteArray")]
        public async Task<IActionResult> AddLogoToPdfFromBase64ByteArray([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequest req)
        {
            try
            {
                // Deserialize the request body into the PdfModificationRequest object
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                PdfModificationRequest data = JsonConvert.DeserializeObject<PdfModificationRequest>(requestBody);

                // Validate the input object
                if (data == null || string.IsNullOrEmpty(data.PdfBytes))
                {
                    _logger.LogError($"The request body must contain a valid base64 PDF byte array.");
                    return new BadRequestObjectResult("The request body must contain a valid base64 PDF byte array.");
                }

                // Convert the base64 string back to a byte array
                byte[] pdfBytes = Convert.FromBase64String(data.PdfBytes);

                string? footerLogo = !string.IsNullOrEmpty(data.FooterLogo) ? data.FooterLogo : null;

                byte[] modifiedPdfBytes = await AddLogoToPdf_2(pdfBytes, data.HeaderLogo, footerLogo);

                // Convert the modified PDF byte array back to base64 for the response
                string base64ModifiedPdf = Convert.ToBase64String(modifiedPdfBytes);

                // Return the modified PDF as a base64-encoded string in JSON format
                return new OkObjectResult(new { pdfBytes = base64ModifiedPdf });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error while processing the PDF: {ex.Message}");
                return new BadRequestObjectResult($"Error: {ex.Message}");
            }
        }


        private async Task<byte[]> AddLogoToPdf_2(byte[] pdfBytes, string headerLogoBase64, string? footerLogoBase64)
        {
            try
            {
                using (MemoryStream pdfStream = new MemoryStream(pdfBytes))
                {
                    using (PdfDocumentProcessor pdfDocumentProcessor = new PdfDocumentProcessor())
                    {
                        pdfDocumentProcessor.LoadDocument(pdfStream);

                        // Add logos to each page of the PDF
                        for (int pageIndex = 0; pageIndex < pdfDocumentProcessor.Document.Pages.Count; pageIndex++)
                        {
                            // Add header logo if provided
                            if (!string.IsNullOrEmpty(headerLogoBase64))
                            {
                                AddBase64ImageToPdfPage(pdfDocumentProcessor, pageIndex, headerLogoBase64, 50, 27, 144, 40, true);
                            }

                            // Add footer logo if provided
                            if (!string.IsNullOrEmpty(footerLogoBase64))
                            {
                                AddBase64ImageToPdfPage(pdfDocumentProcessor, pageIndex, footerLogoBase64, 50, 1075, 40, 40, true);
                            }
                        }

                        using (MemoryStream modifiedPdfStream = new MemoryStream())
                        {
                            pdfDocumentProcessor.SaveDocument(modifiedPdfStream);
                            return modifiedPdfStream.ToArray();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error while processing the PDF: {ex.Message}", ex);
            }
        }

        private void AddBase64ImageToPdfPage(PdfDocumentProcessor pdfDocumentProcessor, int pageIndex, string base64Image, float x, float y, float maxWidth, float maxHeight, bool cover = false)
        {
            // Decode Base64 string into a byte array
            byte[] imageBytes = Convert.FromBase64String(base64Image);

            // Convert byte array into an image
            using (MemoryStream ms = new MemoryStream(imageBytes))
            {
                using (System.Drawing.Image image = System.Drawing.Image.FromStream(ms))
                {
                    // Get the original dimensions of the image
                    float imageOriginalWidth = image.Width;
                    float imageOriginalHeight = image.Height;

                    // Calculate the scaling factor to fit the image within the maxWidth and maxHeight while preserving the aspect ratio
                    float widthScale = maxWidth / imageOriginalWidth;
                    float heightScale = maxHeight / imageOriginalHeight;
                    float scale = Math.Min(widthScale, heightScale);

                    // Scale the image based on the calculated factor
                    float scaledWidth = imageOriginalWidth * scale;
                    float scaledHeight = imageOriginalHeight * scale;

                    // Calculate the position (x, y) to center the image if needed
                    float centeredX = x + (maxWidth - scaledWidth) / 2;
                    float centeredY = y + (maxHeight - scaledHeight) / 2;

                    // Create a graphics context for drawing images
                    using (DevExpress.Pdf.PdfGraphics graphics = pdfDocumentProcessor.CreateGraphics())
                    {
                        // Define the rectangle where the image will be placed
                        System.Drawing.RectangleF imageRect = new System.Drawing.RectangleF(centeredX, centeredY, scaledWidth, scaledHeight);

                        if (cover)
                        {
                            // Draw a white rectangle as the background
                            using (System.Drawing.Brush whiteBrush = new System.Drawing.SolidBrush(System.Drawing.Color.White))
                            {
                                graphics.FillRectangle(whiteBrush, imageRect);
                            }
                        }

                        // Draw the image on top of the white background (if cover = true)
                        graphics.DrawImage(image, imageRect);

                        // Finalize the drawing operation by adding it to the page
                        graphics.AddToPageForeground(pdfDocumentProcessor.Document.Pages[pageIndex]);
                    }
                }
            }
        }


        [Function("FillPdfWithWhiteRectangle")]
        public async Task<IActionResult> FillPdfWithWhiteRectangle([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequest req)
        {
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);

            string blobName = data?.blobName;
            int pageIndex = data?.pageIndex;
            float x = data?.x;
            float y = data?.y;
            float width = data?.width;
            float height = data?.height;
            bool allPages = data?.allPages ?? false;
            bool production = data?.production ?? false;


            if (string.IsNullOrEmpty(blobName))
            {
                return new BadRequestObjectResult("blobName is required.");
            }

            try
            {

                string connectionString = production ? _connectionString : _connectionString_DEV;

                BlobServiceClient blobServiceClient = new BlobServiceClient(connectionString);
                BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(_contractPdfsContainerName);
                BlobClient blobClient = containerClient.GetBlobClient(blobName);

                // Download the blob content into a MemoryStream
                using (MemoryStream pdfStream = new MemoryStream())
                {
                    BlobDownloadInfo download = await blobClient.DownloadAsync();
                    await download.Content.CopyToAsync(pdfStream);
                    pdfStream.Position = 0; // Reset stream position for reading

                    // Load the PDF for modification
                    using (PdfDocumentProcessor pdfDocumentProcessor = new PdfDocumentProcessor())
                    {
                        pdfDocumentProcessor.LoadDocument(pdfStream);

                        if (allPages)
                        {
                            // Fill a white rectangle on all pages
                            for (int i = 0; i < pdfDocumentProcessor.Document.Pages.Count; i++)
                            {
                                FillWhiteRectangleOnPdfPage(pdfDocumentProcessor, i, x, y, width, height);
                            }
                        }
                        else
                        {
                            // Validate pageIndex
                            if (pageIndex < 0 || pageIndex >= pdfDocumentProcessor.Document.Pages.Count)
                            {
                                return new BadRequestObjectResult("Invalid pageIndex.");
                            }
                            // Fill a white rectangle on the specified page
                            FillWhiteRectangleOnPdfPage(pdfDocumentProcessor, pageIndex, x, y, width, height);
                        }

                        // Save the updated PDF to a MemoryStream
                        using (MemoryStream updatedPdfStream = new MemoryStream())
                        {
                            pdfDocumentProcessor.SaveDocument(updatedPdfStream);
                            updatedPdfStream.Position = 0; // Reset stream position for uploading

                            // Upload the modified PDF back to Blob Storage
                            await blobClient.UploadAsync(updatedPdfStream, overwrite: true);
                        }
                    }
                }

                return new OkObjectResult($"PDF has been updated and re-uploaded as modified-{blobName}");
            }
            catch (Exception ex)
            {
                return new BadRequestObjectResult($"Error: {ex.Message}");
            }
        }

        private void FillWhiteRectangleOnPdfPage(PdfDocumentProcessor pdfDocumentProcessor, int pageIndex, float x, float y, float width, float height)
        {
            // Create a graphics context for drawing shapes
            using (DevExpress.Pdf.PdfGraphics graphics = pdfDocumentProcessor.CreateGraphics())
            {
                // Create a white brush to fill the rectangle area
                using (var brush = new DXSolidBrush(DXColor.White))
                {
                    // Define the rectangle where the white area will be placed
                    System.Drawing.RectangleF fillRect = new System.Drawing.RectangleF(x, y, width, height);

                    // Fill the defined area with white
                    graphics.FillRectangle(brush, fillRect);

                    // Finalize the drawing operation by adding it to the page
                    graphics.AddToPageForeground(pdfDocumentProcessor.Document.Pages[pageIndex]);
                }
            }
        }
    }
}
