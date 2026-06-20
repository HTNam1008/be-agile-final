using Moe.Modules.CourseBilling.Contracts.AdminCourses;
using Moe.Modules.CourseBilling.Domain.Courses;

namespace Moe.Modules.CourseBilling.Application.AdminCourses.Materials;

internal static class CourseMaterialMapper
{
    public static CourseMaterialDto ToMaterialDto(CourseMaterial material)
        => new(
            material.Id,
            material.CourseId,
            material.MaterialTitle,
            material.MaterialDescription,
            material.MaterialTypeCode,
            material.FileName,
            material.OriginalFileName,
            material.FileExtension,
            material.ContentType,
            material.FileSizeBytes,
            material.StorageProviderCode,
            material.StoragePath,
            material.PublicUrl,
            material.DisplayOrder,
            material.IsRequired,
            material.IsActive,
            material.UploadedAtUtc,
            material.UpdatedAtUtc,
            material.DeletedAtUtc);
}
