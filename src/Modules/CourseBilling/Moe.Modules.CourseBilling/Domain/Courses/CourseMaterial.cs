using Moe.SharedKernel.Domain;

namespace Moe.Modules.CourseBilling.Domain.Courses;

internal sealed class CourseMaterial : Entity<long>
{
    private CourseMaterial() : base(0) { }

    public CourseMaterial(
        long courseId,
        string materialTitle,
        string? materialDescription,
        string materialTypeCode,
        string fileName,
        string originalFileName,
        string fileExtension,
        string contentType,
        long fileSizeBytes,
        string storageProviderCode,
        string storagePath,
        string? publicUrl,
        int displayOrder,
        bool isRequired,
        DateTime utcNow) : base(0)
    {
        CourseId = courseId;
        MaterialTitle = materialTitle.Trim();
        MaterialDescription = string.IsNullOrWhiteSpace(materialDescription) ? null : materialDescription.Trim();
        MaterialTypeCode = materialTypeCode.Trim();
        FileName = fileName;
        OriginalFileName = originalFileName;
        FileExtension = fileExtension;
        ContentType = contentType;
        FileSizeBytes = fileSizeBytes;
        StorageProviderCode = storageProviderCode;
        StoragePath = storagePath;
        PublicUrl = publicUrl;
        DisplayOrder = displayOrder;
        IsRequired = isRequired;
        IsActive = true;
        UploadedAtUtc = utcNow;
    }

    public long CourseId { get; private set; }
    public string MaterialTitle { get; private set; } = string.Empty;
    public string? MaterialDescription { get; private set; }
    public string MaterialTypeCode { get; private set; } = string.Empty;
    public string FileName { get; private set; } = string.Empty;
    public string OriginalFileName { get; private set; } = string.Empty;
    public string FileExtension { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long FileSizeBytes { get; private set; }
    public string StorageProviderCode { get; private set; } = string.Empty;
    public string StoragePath { get; private set; } = string.Empty;
    public string? PublicUrl { get; private set; }
    public int DisplayOrder { get; private set; }
    public bool IsRequired { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime UploadedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }
    public DateTime? DeletedAtUtc { get; private set; }

    public void UpdateMetadata(
        string materialTitle,
        string? materialDescription,
        string materialTypeCode,
        int displayOrder,
        bool isRequired,
        DateTime utcNow)
    {
        MaterialTitle = materialTitle.Trim();
        MaterialDescription = string.IsNullOrWhiteSpace(materialDescription) ? null : materialDescription.Trim();
        MaterialTypeCode = materialTypeCode.Trim();
        DisplayOrder = displayOrder;
        IsRequired = isRequired;
        UpdatedAtUtc = utcNow;
    }

    public void ReplaceFile(
        string fileName,
        string originalFileName,
        string fileExtension,
        string contentType,
        long fileSizeBytes,
        string storageProviderCode,
        string storagePath,
        string? publicUrl,
        DateTime utcNow)
    {
        FileName = fileName;
        OriginalFileName = originalFileName;
        FileExtension = fileExtension;
        ContentType = contentType;
        FileSizeBytes = fileSizeBytes;
        StorageProviderCode = storageProviderCode;
        StoragePath = storagePath;
        PublicUrl = publicUrl;
        UpdatedAtUtc = utcNow;
    }

    public void SoftDelete(DateTime utcNow)
    {
        IsActive = false;
        DeletedAtUtc = utcNow;
        UpdatedAtUtc = utcNow;
    }
}
