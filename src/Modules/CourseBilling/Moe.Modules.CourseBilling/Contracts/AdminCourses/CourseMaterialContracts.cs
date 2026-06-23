using Microsoft.AspNetCore.Http;

namespace Moe.Modules.CourseBilling.Contracts.AdminCourses;

public sealed record CreateCourseMaterialRequest(
    string MaterialTitle,
    string? MaterialDescription,
    string MaterialTypeCode,
    int DisplayOrder,
    bool IsRequired,
    IFormFile? File);

public sealed record UpdateCourseMaterialRequest(
    string MaterialTitle,
    string? MaterialDescription,
    string MaterialTypeCode,
    int DisplayOrder,
    bool IsRequired);

public sealed record ReplaceCourseMaterialFileRequest(IFormFile? File);

public sealed record CourseMaterialDto(
    long CourseMaterialId,
    long CourseId,
    string MaterialTitle,
    string? MaterialDescription,
    string MaterialTypeCode,
    string FileName,
    string OriginalFileName,
    string FileExtension,
    string ContentType,
    long FileSizeBytes,
    string StorageProviderCode,
    string StoragePath,
    string? PublicUrl,
    int DisplayOrder,
    bool IsRequired,
    bool IsActive,
    DateTime UploadedAt,
    DateTime? UpdatedAt,
    DateTime? DeletedAt);
