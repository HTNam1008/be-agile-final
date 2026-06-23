namespace Moe.Modules.CourseBilling.Contracts.AdminCourses;

public sealed record CourseSummaryDto(
    long CourseId,
    long OrganizationId,
    string CourseCode,
    string CourseName,
    string? Description,
    DateOnly StartDate,
    DateOnly EndDate,
    DateTime EnrollmentOpenAt,
    DateTime EnrollmentCloseAt,
    decimal BeforeStartRefundPercentage,
    decimal AfterStartRefundPercentage,
    string CourseStatusCode,
    decimal TotalFeeAmount,
    int EnrollmentCount,
    DateTime? UpdatedAt,
    DateTime? DisabledAt);

public sealed record CourseDetailDto(
    long CourseId,
    long OrganizationId,
    string CourseCode,
    string CourseName,
    string? Description,
    DateOnly StartDate,
    DateOnly EndDate,
    DateTime EnrollmentOpenAt,
    DateTime EnrollmentCloseAt,
    decimal BeforeStartRefundPercentage,
    decimal AfterStartRefundPercentage,
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
