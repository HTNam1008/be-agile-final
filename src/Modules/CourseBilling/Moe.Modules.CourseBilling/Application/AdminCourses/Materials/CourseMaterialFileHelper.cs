using Microsoft.AspNetCore.Http;
using Moe.Modules.CourseBilling.Domain.Courses;
using Moe.Modules.CourseBilling.IGateway.Storage;

namespace Moe.Modules.CourseBilling.Application.AdminCourses.Materials;

internal static class CourseMaterialFileHelper
{
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
