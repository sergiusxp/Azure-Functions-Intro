using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.Azure.WebJobs.Host;
using IronPdf;
using System.Net.Http;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace ConvertPdf
{
    public static class FunctionPdf
    {
        [FunctionName("HtmlToPdf")]
        public static async Task<string> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post")]
            ConvertPdfRequest request, ILogger log)
        {
            log.LogInformation($"Converting {request.Url} to PDF");

            if (request.Url == null && request.Html == null)
            {
                throw new InvalidOperationException($"Request failed. Please specify either the Html or the resource Url.");
            }

            var html = request.Html == null ? await FetchHtml(request.Url) : request.Html;
            var pdfBytes = BuildPdf(html);
            var response = BuildResponse(pdfBytes);
            var uri = await SaveToBlobStorage(pdfBytes);

            return uri;
        }

        private static async Task<string> SaveToBlobStorage(byte[] pdfBytes)
        {
            var connectionString = System.Environment.GetEnvironmentVariable($"ConnectionStrings:AzStorage");
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(connectionString);
            CloudBlobClient client = storageAccount.CreateCloudBlobClient();
            CloudBlobContainer container = client.GetContainerReference("pdfs");
            
            await container.CreateIfNotExistsAsync();

            CloudBlockBlob blob;
            string name;
            name = string.Format("{0}.pdf", Guid.NewGuid().ToString("n"));
            
            blob = container.GetBlockBlobReference(name);
            blob.Properties.ContentType = "application/pdf";

            using (Stream stream = new MemoryStream(pdfBytes))
            {
                await blob.UploadFromStreamAsync(stream);
                return blob.Uri.AbsoluteUri;
            }
        }

        private static FileContentResult BuildResponse(byte[] pdfBytes)
        {
            return new FileContentResult(pdfBytes, "application/pdf");
        }

        private static byte[] BuildPdf(string html)
        {
            var pdfEngine = renderer.RenderHtmlAsPdf(html);
            return pdfEngine.BinaryData;
        }

        private static async Task<string> FetchHtml(string url)
        {
            var response = await httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"FetchHtml failed {response.StatusCode} : {response.ReasonPhrase}");
            }
            return await response.Content.ReadAsStringAsync();
        }

        static HttpClient httpClient = new HttpClient();
        static HtmlToPdf renderer = new HtmlToPdf();
    }
}
