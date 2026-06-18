using Microsoft.AspNetCore.Hosting;
using Moe.Modules.CourseBilling.IGateway.Storage;

namespace Moe.Modules.CourseBilling.Infrastructure.Storage;

internal sealed class LocalCourseMaterialStorageService(IWebHostEnvironment environment) : ICourseMaterialStorageService
{
    public async Task<StoredCourseMaterialFile> SaveAsync(
        long courseId,
        string originalFileName,
        string contentType,
        Stream content,
        CancellationToken cancellationToken)
    {
        // Temporary local development storage. Replace with the shared storage provider when it is available.
        string extension = Path.GetExtension(originalFileName);
        string storedName = $"{Guid.NewGuid():N}{extension}";
        string relativePath = Path.Combine("course-materials", courseId.ToString(), storedName);
        string root = string.IsNullOrWhiteSpace(environment.WebRootPath)
            ? Path.Combine(environment.ContentRootPath, "wwwroot")
            : environment.WebRootPath;
        string absolutePath = Path.Combine(root, relativePath);

        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);

        await using FileStream output = File.Create(absolutePath);
        await content.CopyToAsync(output, cancellationToken);

        return new StoredCourseMaterialFile(
            storedName,
            originalFileName,
            extension,
            string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
            output.Length,
            "LOCAL_DEV",
            relativePath.Replace('\\', '/'),
            null);
    }
}
