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

    public Task<Stream> OpenReadAsync(string storagePath, CancellationToken cancellationToken)
    {
        string root = string.IsNullOrWhiteSpace(environment.WebRootPath)
            ? Path.Combine(environment.ContentRootPath, "wwwroot")
            : environment.WebRootPath;
        string absoluteRoot = Path.GetFullPath(root);
        string absolutePath = Path.GetFullPath(Path.Combine(absoluteRoot, storagePath));
        if (!absolutePath.StartsWith(absoluteRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Course material path is outside the configured storage root.");

        Stream stream = new FileStream(
            absolutePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            useAsync: true);
        return Task.FromResult(stream);
    }
}
