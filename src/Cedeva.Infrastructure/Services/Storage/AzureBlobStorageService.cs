using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Cedeva.Core.Interfaces;
using Cedeva.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace Cedeva.Infrastructure.Services.Storage;

public class AzureBlobStorageService : IStorageService
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly string _containerName;

    public AzureBlobStorageService(IOptions<AzureStorageOptions> options)
    {
        var storage = options.Value;
        if (string.IsNullOrWhiteSpace(storage.ConnectionString))
            throw new InvalidOperationException("Azure Storage connection string not configured");

        _containerName = string.IsNullOrWhiteSpace(storage.ContainerName) ? "cedeva-files" : storage.ContainerName;
        _blobServiceClient = new BlobServiceClient(storage.ConnectionString);
    }

    public async Task<string> UploadFileAsync(Stream fileStream, string fileName, string contentType, string containerPath)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
        await containerClient.CreateIfNotExistsAsync(PublicAccessType.None);

        var blobPath = string.IsNullOrEmpty(containerPath)
            ? fileName
            : $"{containerPath.TrimEnd('/')}/{fileName}";

        var blobClient = containerClient.GetBlobClient(blobPath);

        var blobHttpHeaders = new BlobHttpHeaders { ContentType = contentType };
        await blobClient.UploadAsync(fileStream, new BlobUploadOptions { HttpHeaders = blobHttpHeaders });

        return blobPath;
    }

    public async Task<Stream?> DownloadFileAsync(string filePath)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
        var blobClient = containerClient.GetBlobClient(filePath);

        if (!await blobClient.ExistsAsync())
            return null;

        var response = await blobClient.DownloadAsync();
        return response.Value.Content;
    }

    public async Task DeleteFileAsync(string filePath)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
        var blobClient = containerClient.GetBlobClient(filePath);
        await blobClient.DeleteIfExistsAsync();
    }

    public string GetFileUrl(string filePath)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
        var blobClient = containerClient.GetBlobClient(filePath);
        return blobClient.Uri.ToString();
    }
}
