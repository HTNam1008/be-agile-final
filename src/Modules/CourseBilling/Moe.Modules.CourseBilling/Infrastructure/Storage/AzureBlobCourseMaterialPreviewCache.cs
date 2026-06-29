using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moe.Modules.CourseBilling.Domain.Courses;
using Moe.Modules.CourseBilling.IGateway.Storage;

namespace Moe.Modules.CourseBilling.Infrastructure.Storage;

internal sealed class AzureBlobCourseMaterialPreviewCache : ICourseMaterialPreviewCache
{
    private readonly BlobContainerClient container;
    private readonly ILogger<AzureBlobCourseMaterialPreviewCache> logger;

    public AzureBlobCourseMaterialPreviewCache(
        IConfiguration configuration,
        ILogger<AzureBlobCourseMaterialPreviewCache> logger)
    {
        string connectionString = configuration["AzureBlob:ConnectionString"]
            ?? throw new InvalidOperationException("AzureBlob:ConnectionString is required.");
        string containerName = configuration["AzureBlob:ContainerName"]
            ?? throw new InvalidOperationException("AzureBlob:ContainerName is required.");
        container = new BlobContainerClient(connectionString, containerName);
        this.logger = logger;
    }

    public async Task<Stream?> GetPdfAsync(CourseMaterial material, CancellationToken cancellationToken)
    {
        BlobClient preview = container.GetBlobClient(PreviewPath(material));
        try
        {
            if (!(await preview.ExistsAsync(cancellationToken)).Value)
                return null;
            return (await preview.DownloadStreamingAsync(cancellationToken: cancellationToken)).Value.Content;
        }
        catch (RequestFailedException exception)
        {
            logger.LogWarning(
                exception,
                "Could not read the persistent PDF preview for course material {CourseMaterialId}.",
                material.Id);
            return null;
        }
    }

    public async Task SetPdfAsync(
        CourseMaterial material,
        byte[] pdfBytes,
        CancellationToken cancellationToken)
    {
        if (pdfBytes.Length == 0)
            return;

        BlobClient preview = container.GetBlobClient(PreviewPath(material));
        try
        {
            await preview.UploadAsync(
                BinaryData.FromBytes(pdfBytes),
                new BlobUploadOptions
                {
                    HttpHeaders = new BlobHttpHeaders { ContentType = "application/pdf" }
                },
                cancellationToken);
        }
        catch (RequestFailedException exception)
        {
            logger.LogWarning(
                exception,
                "Could not persist the PDF preview for course material {CourseMaterialId}.",
                material.Id);
        }
    }

    internal static string PreviewPath(CourseMaterial material)
        => $"{material.StoragePath.Replace('\\', '/')}.preview.pdf";
}
