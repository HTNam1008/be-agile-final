using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Microsoft.Extensions.Configuration;
using Moe.Modules.CourseBilling.IGateway.Storage;

namespace Moe.Modules.CourseBilling.Infrastructure.Storage;

internal sealed class AzureBlobCourseMaterialStorageService : ICourseMaterialStorageService
{
    private const string StorageProviderCode = "AZURE_BLOB";

    private readonly BlobContainerClient container;

    public AzureBlobCourseMaterialStorageService(IConfiguration configuration)
    {
        string connectionString = configuration["AzureBlob:ConnectionString"]
            ?? throw new InvalidOperationException("AzureBlob:ConnectionString is required.");
        string containerName = configuration["AzureBlob:ContainerName"]
            ?? throw new InvalidOperationException("AzureBlob:ContainerName is required.");

        container = new BlobContainerClient(connectionString, containerName);
    }

    public async Task<StoredCourseMaterialFile> SaveAsync(
        long courseId,
        string originalFileName,
        string contentType,
        Stream content,
        CancellationToken cancellationToken)
    {
        string extension = Path.GetExtension(originalFileName);
        string storedName = $"{Guid.NewGuid():N}{extension}";
        string blobName = $"course-materials/{courseId}/{storedName}";
        string normalizedContentType = string.IsNullOrWhiteSpace(contentType)
            ? "application/octet-stream"
            : contentType;

        BlobClient blob = container.GetBlobClient(blobName);
        await blob.UploadAsync(
            content,
            new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = normalizedContentType
                }
            },
            cancellationToken);

        BlobProperties properties = await blob.GetPropertiesAsync(cancellationToken: cancellationToken);

        return new StoredCourseMaterialFile(
            storedName,
            originalFileName,
            extension,
            normalizedContentType,
            properties.ContentLength,
            StorageProviderCode,
            blobName,
            null);
    }

    public async Task<Stream> OpenReadAsync(string storagePath, CancellationToken cancellationToken)
    {
        BlobClient blob = container.GetBlobClient(storagePath.Replace('\\', '/'));
        return (await blob.DownloadStreamingAsync(cancellationToken: cancellationToken)).Value.Content;
    }

    public Task<Uri?> CreateReadUriAsync(
        string storagePath,
        DateTimeOffset expiresAtUtc,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        BlobClient blob = container.GetBlobClient(storagePath.Replace('\\', '/'));
        Uri? readUri = blob.CanGenerateSasUri
            ? blob.GenerateSasUri(BlobSasPermissions.Read, expiresAtUtc)
            : null;
        return Task.FromResult(readUri);
    }
}
