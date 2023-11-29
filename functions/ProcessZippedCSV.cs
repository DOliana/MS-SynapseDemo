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
            [HttpTrigger(AuthorizationLevel.Function, "put", Route = null)] HttpRequest req,
            ILogger log)
        {
            string sourceFilepath = req.Query["sourceFilepath"];
            string sourceContainerName = req.Query["sourceContainerName"];
            string targetContainerName = req.Query["targetContainerName"];

            log.LogInformation($"processing file {sourceFilepath} in countainer {sourceContainerName}");


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
                if(await zippedBlob.ExistsAsync() == false) { throw new FileNotFoundException("file does not exist", sourceFilepath); };
                log.LogInformation($"{sourceFilepath} exists, unzipping it");

                using var archive = new ZipArchive(await zippedBlob.OpenReadAsync());
                var files = new List<string>();
                foreach (var entry in archive.Entries)
                {
                    log.LogInformation($"processing zipped entry {entry.FullName}");
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
                log.LogError(ex, ex.Message);
                return new ObjectResult(ex.Message)
                {
                    StatusCode = (int)HttpStatusCode.InternalServerError
                };
            }
        }
    }
}
