using Microsoft.AspNetCore.Http;
using Moe.Infrastructure.Shared.Api;
using Moe.SharedKernel.Results;

namespace Moe.Modules.CourseBilling.Application.AdminCourses;

public sealed record CourseQueryRequest(
    string? Keyword,
    string? StatusCode,
    int Page = 1,
    int PageSize = 20);

public sealed record CreateCourseRequest(
    string CourseCode,
    string CourseName,
    string? Description,
    DateOnly StartDate,
    DateOnly EndDate,
    DateTime EnrollmentCloseAt);

public sealed record UpdateCourseRequest(
    string CourseCode,
    string CourseName,
    string? Description,
    DateOnly StartDate,
    DateOnly EndDate,
    DateTime EnrollmentCloseAt);

public sealed record DisableCourseRequest;

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

public sealed record CreateCourseFeeRequest(long FeeComponentId, decimal FeeValue, int SequenceNumber);

public sealed record UpdateCourseFeeRequest(decimal FeeValue, int SequenceNumber);

public sealed record AssignStudentsToCourseRequest(IReadOnlyList<long> PersonIds);

public sealed record CourseSummaryDto(
    long CourseId,
    string CourseCode,
    string CourseName,
    string? Description,
    DateOnly StartDate,
    DateOnly EndDate,
    DateTime EnrollmentOpenAt,
    DateTime EnrollmentCloseAt,
    string CourseStatusCode,
    DateTime? UpdatedAt,
    DateTime? DisabledAt);

public sealed record CourseDetailDto(
    long CourseId,
    string CourseCode,
    string CourseName,
    string? Description,
    DateOnly StartDate,
    DateOnly EndDate,
    DateTime EnrollmentOpenAt,
    DateTime EnrollmentCloseAt,
    string CourseStatusCode,
    DateTime? UpdatedAt,
    DateTime? DisabledAt,
    IReadOnlyList<CourseMaterialDto> Materials,
    IReadOnlyList<CourseFeeDto> Fees,
    CourseEnrollmentSummaryDto EnrollmentSummary,
    CoursePublishReadinessDto PublishReadiness);

public sealed record CoursePreviewDto(
    CourseDetailDto Course,
    decimal TotalFeeAmount,
    int ActiveFeeCount);

public sealed record CoursePublishReadinessDto(
    bool CanPublish,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings,
    string SetupStepCode);

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

public sealed record CourseFeeDto(
    long CourseFeeId,
    long CourseId,
    long FeeComponentId,
    string ComponentCode,
    string ComponentName,
    string ComponentTypeCode,
    string CalculationTypeCode,
    decimal FeeValue,
    int SequenceNumber,
    bool IsActive);

public sealed record CourseEnrollmentSummaryDto(int PendingPaymentCount, int CancelledCount, int TotalCount);

public sealed record AdminCourseEnrollmentDto(
    long CourseEnrollmentId,
    long CourseId,
    long PersonId,
    string? FullName,
    string EnrollmentSourceCode,
    long EnrolledByLoginAccountId,
    DateTime EnrolledAt,
    string EnrollmentStatusCode);

public sealed record AssignStudentsToCourseResultDto(
    long CourseId,
    int TotalRequested,
    int TotalSucceeded,
    int TotalFailed,
    IReadOnlyList<AssignStudentResultDto> Results);

public sealed record AssignStudentResultDto(
    long PersonId,
    bool Success,
    long? CourseEnrollmentId,
    string Message);

public interface IAdminCourseService
{
    Task<Result<PageResponse<CourseSummaryDto>>> ListCoursesAsync(CourseQueryRequest request, CancellationToken cancellationToken);
    Task<Result<CourseDetailDto>> GetCourseAsync(long courseId, CancellationToken cancellationToken);
    Task<Result<CoursePreviewDto>> PreviewCourseAsync(long courseId, CancellationToken cancellationToken);
    Task<Result<CourseDetailDto>> CreateCourseAsync(CreateCourseRequest request, CancellationToken cancellationToken);
    Task<Result<CourseDetailDto>> UpdateCourseAsync(long courseId, UpdateCourseRequest request, CancellationToken cancellationToken);
    Task<Result<CourseDetailDto>> PublishCourseAsync(long courseId, CancellationToken cancellationToken);
    Task<Result<CourseDetailDto>> DisableCourseAsync(long courseId, DisableCourseRequest request, CancellationToken cancellationToken);
    Task<Result<CourseDetailDto>> EnableCourseAsync(long courseId, CancellationToken cancellationToken);
    Task<Result<IReadOnlyList<CourseMaterialDto>>> ListMaterialsAsync(long courseId, CancellationToken cancellationToken);
    Task<Result<CourseMaterialDto>> AddMaterialAsync(long courseId, CreateCourseMaterialRequest request, CancellationToken cancellationToken);
    Task<Result<CourseMaterialDto>> UpdateMaterialAsync(long courseId, long courseMaterialId, UpdateCourseMaterialRequest request, CancellationToken cancellationToken);
    Task<Result<CourseMaterialDto>> ReplaceMaterialFileAsync(long courseId, long courseMaterialId, ReplaceCourseMaterialFileRequest request, CancellationToken cancellationToken);
    Task<Result<CourseMaterialDto>> DeleteMaterialAsync(long courseId, long courseMaterialId, CancellationToken cancellationToken);
    Task<Result<IReadOnlyList<CourseFeeDto>>> ListFeesAsync(long courseId, CancellationToken cancellationToken);
    Task<Result<CourseFeeDto>> AddFeeAsync(long courseId, CreateCourseFeeRequest request, CancellationToken cancellationToken);
    Task<Result<CourseFeeDto>> UpdateFeeAsync(long courseId, long courseFeeId, UpdateCourseFeeRequest request, CancellationToken cancellationToken);
    Task<Result<CourseFeeDto>> DeleteFeeAsync(long courseId, long courseFeeId, CancellationToken cancellationToken);
    Task<Result<AssignStudentsToCourseResultDto>> AssignStudentsAsync(long courseId, AssignStudentsToCourseRequest request, CancellationToken cancellationToken);
    Task<Result<IReadOnlyList<AdminCourseEnrollmentDto>>> ListEnrollmentsAsync(long courseId, CancellationToken cancellationToken);
    Task<Result<AdminCourseEnrollmentDto>> RemoveEnrollmentAsync(long courseId, long courseEnrollmentId, CancellationToken cancellationToken);
}
