namespace Moe.Modules.CourseBilling.Contracts.DeferExtensions;

public sealed record DeferExtensionRequestResponse(
    long RequestId,
    long BillId,
    long CourseEnrollmentId,
    long PersonId,
    long OrganizationId,
    string StatusCode,
    DateTime RequestedAtUtc,
    long RequestedByLoginAccountId,
    DateTime? ReviewedAtUtc,
    long? ReviewedByLoginAccountId,
    DateTime? DeadlineAtUtc,
    string? CourseCode,
    string? CourseName,
    string? BillNumber,
    int? DeferralCount);

public sealed record DeferExtensionRequestQueryRequest(
    long? OrganizationId = null,
    string? StatusCode = null,
    int Page = 1,
    int PageSize = 20);
