namespace Moe.Modules.CourseBilling.IGateway.Storage;

public sealed record StoredCourseMaterialFile(
    string FileName,
    string OriginalFileName,
    string FileExtension,
    string ContentType,
    long FileSizeBytes,
    string StorageProviderCode,
    string StoragePath,
    string? PublicUrl);

public interface ICourseMaterialStorageService
{
    Task<StoredCourseMaterialFile> SaveAsync(
        long courseId,
        string originalFileName,
        string contentType,
        Stream content,
        CancellationToken cancellationToken);
}
