using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Moe.Modules.CourseBilling.Domain.Courses;
using Moe.Modules.CourseBilling.IGateway.Storage;

namespace Moe.Modules.CourseBilling.Infrastructure.Storage;

internal sealed class CompositeCourseMaterialPreviewCache(
    ICourseMaterialPreviewRedisCache redisCache,
    ICourseMaterialPreviewBlobCache blobCache,
    ILogger<CompositeCourseMaterialPreviewCache> logger) : ICourseMaterialPreviewCache
{
    public async Task<Stream?> GetPdfAsync(CourseMaterial material, CancellationToken cancellationToken)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        Stream? redisPreview = await TryGetAsync(redisCache, "Redis", material, cancellationToken);
        if (redisPreview is not null)
        {
            logger.LogInformation(
                "Course material PDF preview cache hit. Source=Redis, CourseMaterialId={CourseMaterialId}, ElapsedMs={ElapsedMs}.",
                material.Id,
                stopwatch.ElapsedMilliseconds);
            return redisPreview;
        }

        Stream? blobPreview = await TryGetAsync(blobCache, "AzureBlob", material, cancellationToken);
        if (blobPreview is null)
        {
            logger.LogInformation(
                "Course material PDF preview cache miss. CourseMaterialId={CourseMaterialId}, ElapsedMs={ElapsedMs}.",
                material.Id,
                stopwatch.ElapsedMilliseconds);
            return null;
        }

        await using (blobPreview)
        {
            byte[] bytes = await ReadAllBytesAsync(blobPreview, cancellationToken);
            logger.LogInformation(
                "Course material PDF preview cache hit. Source=AzureBlob, CourseMaterialId={CourseMaterialId}, SizeBytes={SizeBytes}, ElapsedMs={ElapsedMs}.",
                material.Id,
                bytes.Length,
                stopwatch.ElapsedMilliseconds);
            await TrySetAsync(redisCache, "Redis", material, bytes, cancellationToken);
            return new MemoryStream(bytes, writable: false);
        }
    }

    public async Task SetPdfAsync(CourseMaterial material, byte[] pdfBytes, CancellationToken cancellationToken)
    {
        if (pdfBytes.Length == 0)
            return;

        await TrySetAsync(blobCache, "AzureBlob", material, pdfBytes, cancellationToken);
        await TrySetAsync(redisCache, "Redis", material, pdfBytes, cancellationToken);
    }

    private async Task<Stream?> TryGetAsync(
        ICourseMaterialPreviewCache cache,
        string source,
        CourseMaterial material,
        CancellationToken cancellationToken)
    {
        try
        {
            return await cache.GetPdfAsync(material, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(
                exception,
                "Course material PDF preview cache read failed. Source={Source}, CourseMaterialId={CourseMaterialId}.",
                source,
                material.Id);
            return null;
        }
    }

    private async Task TrySetAsync(
        ICourseMaterialPreviewCache cache,
        string source,
        CourseMaterial material,
        byte[] pdfBytes,
        CancellationToken cancellationToken)
    {
        try
        {
            await cache.SetPdfAsync(material, pdfBytes, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(
                exception,
                "Course material PDF preview cache write failed. Source={Source}, CourseMaterialId={CourseMaterialId}.",
                source,
                material.Id);
        }
    }

    private static async Task<byte[]> ReadAllBytesAsync(Stream stream, CancellationToken cancellationToken)
    {
        await using MemoryStream memory = new();
        await stream.CopyToAsync(memory, cancellationToken);
        return memory.ToArray();
    }
}
