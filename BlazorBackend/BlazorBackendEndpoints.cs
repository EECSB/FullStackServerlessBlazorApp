using System;
using System.IO;
using System.Threading.Tasks;
using System.Net.Http;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Azure;
using Azure.Data.Tables;
using Azure.Storage.Blobs;

namespace BlazorBackend
{
    public class BlazorBackendEndpoints
    {
        private readonly ILogger _logger;
        private readonly HttpClient _client;

        public BlazorBackendEndpoints(ILoggerFactory loggerFactory, IHttpClientFactory httpClientFactory)
        {
            _logger = loggerFactory.CreateLogger<BlazorBackendEndpoints>();
            _client = httpClientFactory.CreateClient();
        }


        #region Endpoints //////////////////////////////////////////////////////////////

        [Function("GetFiles")]
        public async Task<HttpResponseData> GetFiles([HttpTrigger(AuthorizationLevel.Function, "Get")] HttpRequestData req)
        {
            req.Headers.TryGetValues("FileSpace", out var fileSpaceHeader);
            string fileSpace = fileSpaceHeader.First().ToString();

            var files = await getFiles(fileSpace);
            var serializedFiles = JsonSerializer.Serialize(files);

            //Create and return a response.
            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/plain; charset=utf-8");
            response.WriteString(serializedFiles);

            return response;
        }

        [Function("UploadFiles")]
        public async Task<HttpResponseData> UploadFiles([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
        {
            Stream fileContents = req.Body;

            req.Headers.TryGetValues("FileSpace", out var fileSpaceHeader);
            string fileSpace = fileSpaceHeader.First().ToString();

            req.Headers.TryGetValues("FileName", out var fileNameHeader);
            string fileName = fileNameHeader.First().ToString();

            string fileID = Guid.NewGuid().ToString();
            await uploadFile(fileID, fileName, fileSpace, fileContents);

            //Create and return a response.
            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/plain; charset=utf-8");
            response.WriteString("true");

            return response;
        }

        [Function("DeleteFile")]
        public async Task<HttpResponseData> DeleteFile([HttpTrigger(AuthorizationLevel.Function, "delete")] HttpRequestData req)
        {
            req.Headers.TryGetValues("FileSpace", out var fileSpaceHeader);
            string fileSpace = fileSpaceHeader.First().ToString();

            req.Headers.TryGetValues("FileId", out var FileIdHeader);
            string FileId = FileIdHeader.First().ToString();

            await deleteFile(fileSpace, FileId);

            //Create and return a response.
            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/plain; charset=utf-8");
            response.WriteString("true");

            return response;
        }

        #endregion //////////////////////////////////////////////////////////////////////


        #region Methods /////////////////////////////////////////////////////////////////

        private static async Task uploadFile(string fileID, string fileName, string fileSpace, Stream fileContents)
        {
            await blobUpload(fileID, fileContents);
            await addTableFileEntry(fileID, fileName, fileSpace);
        }

        private static async Task blobUpload(string blobName, Stream stream)
        {
            string connectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
            string containerName = "files-container";

            BlobServiceClient blobServiceClient = new BlobServiceClient(connectionString);
            BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(containerName);
            await containerClient.CreateIfNotExistsAsync();
            BlobClient blobClient = containerClient.GetBlobClient(blobName);

            await blobClient.UploadAsync(stream);
        }

        private static async Task addTableFileEntry(string fileID, string fileName, string fileSpace)
        {
            string connectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
            string tableName = "FilesTable";

            TableClient tableClient = new TableClient(connectionString, tableName);
            tableClient.CreateIfNotExists();

            //Create a new entity(table row).
            FilesTable entity = new FilesTable
            {
                PartitionKey = "partition1",
                RowKey = fileID,
                FileName = fileName,
                FileSpace = fileSpace
            };

            tableClient.AddEntity(entity);
        }



        private static async Task<List<CloudFile>> getFiles(string fileSpace)
        {
            string connectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
            string tableName = "FilesTable";

            TableClient tableClient = new TableClient(connectionString, tableName);

            Pageable<FilesTable> entities = tableClient.Query<FilesTable>(filter: $"FileSpace eq '{fileSpace}'"); //This will query all the rows from the table where the partition key is 'partition1'.

            List<CloudFile> files = new List<CloudFile>();
            foreach (FilesTable entity in entities)
            {
                string link = await getBlobUri(entity.RowKey);

                files.Add(new CloudFile(
                    entity.FileName,
                    entity.FileSpace,
                    entity.RowKey,
                    entity.Timestamp.Value.DateTime,
                    link
                ));
            }

            return files;
        }
        
        private static async Task<string> getBlobUri(string blobName)
        {
            string connectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
            string containerName = "files-container";

            BlobServiceClient blobServiceClient = new BlobServiceClient(connectionString);
            BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(containerName);
            BlobClient blobClient = containerClient.GetBlobClient(blobName);

            //Check if the blob exists.
            if (!blobClient.Exists())
                return "";

            string sasURI = blobClient.GenerateSasUri(Azure.Storage.Sas.BlobSasPermissions.Read, DateTimeOffset.Now.AddDays(1)).AbsoluteUri;

            return sasURI;
        }



        private static async Task deleteFile(string fileSpace, string fileId)
        {
            await deleteBlob(fileSpace, fileId);
            await deleteTableEntry(fileSpace, fileId);
        }

        private static async Task deleteBlob(string fileSpace, string fileId)
        {
            string connectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
            string containerName = "files-container";

            BlobServiceClient blobServiceClient = new BlobServiceClient(connectionString);
            BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(containerName);
            BlobClient blobClient = containerClient.GetBlobClient(fileId);

            if (!blobClient.Exists())
                return;
            else
                blobClient.DeleteIfExists();
        }

        private static async Task deleteTableEntry(string fileSpace, string fileId)
        {
            string connectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
            string tableName = "FilesTable";

            TableClient tableClient = new TableClient(connectionString, tableName);

            Pageable<FilesTable> entities = tableClient.Query<FilesTable>(filter: $"FileSpace eq '{fileSpace}' and RowKey eq '{fileId}'");

            foreach (FilesTable entity in entities)
            {
                tableClient.DeleteEntity(entity);
            }
        }

        #endregion //////////////////////////////////////////////////////////////////////


        #region Models //////////////////////////////////////////////////////////////////

        public class FilesTable : ITableEntity
        {
            //Required properties
            public string PartitionKey { get; set; }
            public string RowKey { get; set; }
            public DateTimeOffset? Timestamp { get; set; }
            public ETag ETag { get; set; }

            //Custom properties
            public string FileName { get; set; }
            public string FileSpace { get; set; }
        }

        class CloudFile
        {
            public CloudFile(string name, string fileSpace, string fileID, DateTime timeStamp, string link)
            {
                Name = name;
                FileSpace = fileSpace;
                FileID = fileID;
                TimeStamp = timeStamp;
                Link = link;
            }

            public string Name { get; set; }
            public string FileSpace { get; set; }
            public string FileID { get; set; }
            public DateTime TimeStamp { get; set; }
            public string Link { get; set; }
        }

        #endregion //////////////////////////////////////////////////////////////////////
    }
}
