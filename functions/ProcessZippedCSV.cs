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
using System.Text;

namespace synapse_funcs
{
    public static class ProcessZippedCSV
    {
        const string sourceStorageAccountName = "dolidrop";
        const string targetStorageAccountName = "dolilake";



        [FunctionName("ProcessZippedCSV")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "put", Route = null)] ProcessFilesParameters parameters,
            ILogger log)
        {
            log.LogInformation($"processing file {parameters.SourceFilepath} in path {parameters.SourceContainerName}");

            var sourceStorageClient = new BlobServiceClient(
                new Uri($"https://{sourceStorageAccountName}.blob.core.windows.net"),
                new DefaultAzureCredential());
            var sourceContainerName = parameters.SourceContainerName.Split('/')[0];
            var sourceContainer = sourceStorageClient.GetBlobContainerClient(sourceContainerName);
            var sourceFilePath = String.Join('/', parameters.SourceContainerName.Split('/').Skip(1)) + '/' + parameters.SourceFilepath;

            var targetStorageClient = new BlobServiceClient(
                new Uri($"https://{targetStorageAccountName}.blob.core.windows.net"),
                new DefaultAzureCredential());
            var targetContainer = targetStorageClient.GetBlobContainerClient(parameters.TargetContainerName);

            if (string.IsNullOrWhiteSpace(parameters.RunId)) { throw new InvalidOperationException($"parameter runid must be set"); }


            try
            {
                if (await sourceContainer.ExistsAsync() == false) { throw new DirectoryNotFoundException($"the source container {parameters.SourceContainerName} dose not exist."); }
                if (await targetContainer.ExistsAsync() == false) { throw new DirectoryNotFoundException($"the target container {parameters.TargetContainerName} dose not exist."); }

                var zippedBlob = sourceContainer.GetBlobClient(sourceFilePath);
                if (await zippedBlob.ExistsAsync() == false) { throw new FileNotFoundException("file does not exist", sourceFilePath); };
                log.LogInformation($"{sourceFilePath} exists, unzipping it");

                using var archive = new ZipArchive(await zippedBlob.OpenReadAsync());
                var files = new List<string>();
                foreach (var entry in archive.Entries)
                {
                    log.LogInformation($"processing zipped entry {entry.FullName}");
                    await using var fileStream = entry.Open();
                    var targetPath = Path.Combine(parameters.TargetFolderPath, entry.Name);
                    if (await targetContainer.GetBlobClient(targetPath).DeleteIfExistsAsync()) { log.LogInformation($"target file existed - overwriting. {targetPath}"); }
                    await targetContainer.UploadBlobAsync(targetPath, fileStream);
                    files.Add(targetPath);
                }
                files.Sort();

                var fileListPath = parameters.TargetFolderPath + "/" + parameters.RunId + ".txt";
                var fileList = targetContainer.GetBlobClient(fileListPath);
                using var ms = new MemoryStream(Encoding.UTF8.GetBytes(String.Join(Environment.NewLine, files)));
                await fileList.UploadAsync(ms, overwrite: true);

                return new OkObjectResult(new
                {
                    writtenFileCount = files.Count,
                    files = files,
                    fileList = fileListPath
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
