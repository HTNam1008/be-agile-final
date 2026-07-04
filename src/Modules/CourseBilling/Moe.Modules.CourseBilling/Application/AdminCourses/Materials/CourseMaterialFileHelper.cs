using Microsoft.AspNetCore.Http;
using Moe.Modules.CourseBilling.Domain.Courses;
using Moe.Modules.CourseBilling.IGateway.Storage;

namespace Moe.Modules.CourseBilling.Application.AdminCourses.Materials;

internal static class CourseMaterialFileHelper
{
    public const long MaxFileSizeBytes = 20 * 1024 * 1024;
    private static readonly HashSet<string> AllowedExtensions =
        [".pdf", ".docx", ".pptx", ".png", ".jpg", ".jpeg"];

    public static bool ExceedsMaxFileSize(IFormFile file)
        => file.Length > MaxFileSizeBytes;

    public static bool IsSupported(IFormFile file)
        => AllowedExtensions.Contains(Path.GetExtension(file.FileName).ToLowerInvariant());

    public static async Task<StoredCourseMaterialFile> StoreFileAsync(
        ICourseMaterialStorageService storage,
        long courseId,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        await using Stream stream = file.OpenReadStream();
        return await storage.SaveAsync(courseId, file.FileName, file.ContentType, stream, cancellationToken);
    }
}
