using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.IO.Compression;
using Microsoft.WindowsAzure.Storage;
using Azure.Identity;
using Azure.Storage.Blobs;
using System.Collections.Generic;
using System.Linq;
using System.Web.Http;
using System.Net;

namespace synapse_funcs
{
    public static class ProcessZippedCSV
    {
        const string sourceStorageAccountName = "dolidrop";
        const string targetStorageAccountName = "dolilake";



        [FunctionName("ProcessZippedCSV")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "put", Route = "?sourceContainer={sourceContainerName}&sourceFilepath={sourceFilepath}&targetContainer={targetContainerName}")] HttpRequest req,
            string sourceFilepath,
            string sourceContainerName,
            string targetContainerName,
            ILogger log)
        {
            log.LogInformation($"checking file {sourceFilepath}");

            var sourceStorageClient = new BlobServiceClient(
                new Uri($"https://{sourceStorageAccountName}.blob.core.windows.net"),
                new DefaultAzureCredential());
            var sourceContainer = sourceStorageClient.GetBlobContainerClient(sourceContainerName);
            var targetStorageClient = new BlobServiceClient(
                new Uri($"https://{targetStorageAccountName}.blob.core.windows.net"),
                new DefaultAzureCredential());
            var targetContainer = targetStorageClient.GetBlobContainerClient(targetContainerName);


            try
            {
                var zippedBlob = sourceContainer.GetBlobClient(sourceFilepath);
                using ZipArchive archive = new ZipArchive(await zippedBlob.OpenReadAsync());
                List<string> files = new List<string>();
                foreach (var entry in archive.Entries)
                {
                    await using var fileStream = entry.Open();
                    await targetContainer.UploadBlobAsync(entry.Name, fileStream);
                    files.Add(entry.Name);
                }
                files.Sort();

                return new OkObjectResult(new
                {
                    writtenFileCount = files.Count,
                    files = files
                });
            }
            catch (Exception ex)
            {
                return new ObjectResult(ex.Message)
                {
                    StatusCode = (int)HttpStatusCode.InternalServerError
                };
            }
        }
    }
}