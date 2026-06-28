using Moe.Infrastructure.Shared.Api;
using Moe.Modules.CourseBilling.Domain.Billing;

namespace Moe.Modules.CourseBilling.IGateway.Repositories;

internal sealed record DeferExtensionBillSnapshot(
    long BillId,
    string BillNumber,
    long CourseEnrollmentId,
    long PersonId,
    long OrganizationId,
    long CourseId,
    string CourseCode,
    string CourseName,
    int DeferralCount,
    bool IsDeferExtensionGranted,
    decimal OutstandingAmount,
    string BillStatusCode);

internal sealed record DeferExtensionReviewAggregate(
    DeferExtensionRequest Request,
    Bill Bill);

internal sealed record DeferExtensionRequestProjection(
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
    string CourseCode,
    string CourseName,
    string BillNumber,
    int DeferralCount);

internal interface IDeferExtensionRequestRepository
{
    Task<DeferExtensionBillSnapshot?> FindBillForStudentAsync(
        long billId,
        long personId,
        CancellationToken cancellationToken);

    Task<bool> HasPendingRequestAsync(long billId, CancellationToken cancellationToken);

    Task AddAsync(DeferExtensionRequest request, CancellationToken cancellationToken);

    Task<DeferExtensionReviewAggregate?> FindForReviewAsync(
        long requestId,
        CancellationToken cancellationToken);

    Task<PageResponse<DeferExtensionRequestProjection>> ListAsync(
        long? organizationId,
        IReadOnlyCollection<long> scopedOrganizationIds,
        bool hasGlobalAccess,
        string? statusCode,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
