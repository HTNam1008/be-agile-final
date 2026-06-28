using Moe.SharedKernel.Domain;
using Moe.SharedKernel.Results;

namespace Moe.Modules.CourseBilling.Domain.Billing;

internal sealed class DeferExtensionRequest : Entity<long>
{
    private DeferExtensionRequest() : base(0) { }

    private DeferExtensionRequest(
        long billId,
        long courseEnrollmentId,
        long personId,
        long organizationId,
        long requestedByLoginAccountId,
        DateTime requestedAtUtc) : base(0)
    {
        BillId = billId;
        CourseEnrollmentId = courseEnrollmentId;
        PersonId = personId;
        OrganizationId = organizationId;
        StatusCode = DeferExtensionRequestStatusCodes.Pending;
        RequestedByLoginAccountId = requestedByLoginAccountId;
        RequestedAtUtc = requestedAtUtc;
    }

    public long BillId { get; private set; }
    public long CourseEnrollmentId { get; private set; }
    public long PersonId { get; private set; }
    public long OrganizationId { get; private set; }
    public string StatusCode { get; private set; } = string.Empty;
    public long RequestedByLoginAccountId { get; private set; }
    public DateTime RequestedAtUtc { get; private set; }
    public long? ReviewedByLoginAccountId { get; private set; }
    public DateTime? ReviewedAtUtc { get; private set; }
    public DateTime? DeadlineAtUtc { get; private set; }

    public static Result<DeferExtensionRequest> Create(
        long billId,
        long courseEnrollmentId,
        long personId,
        long organizationId,
        long requestedByLoginAccountId,
        DateTime requestedAtUtc)
    {
        if (billId <= 0 || courseEnrollmentId <= 0 || personId <= 0 ||
            organizationId <= 0 || requestedByLoginAccountId <= 0)
        {
            return Result<DeferExtensionRequest>.Failure(BillingErrors.InvalidDeferExtensionRequest);
        }

        return Result<DeferExtensionRequest>.Success(new(
            billId,
            courseEnrollmentId,
            personId,
            organizationId,
            requestedByLoginAccountId,
            requestedAtUtc));
    }

    public Result Approve(long reviewedByLoginAccountId, DateTime reviewedAtUtc)
    {
        if (reviewedByLoginAccountId <= 0 || StatusCode != DeferExtensionRequestStatusCodes.Pending)
            return Result.Failure(BillingErrors.InvalidDeferExtensionRequest);

        StatusCode = DeferExtensionRequestStatusCodes.Approved;
        ReviewedByLoginAccountId = reviewedByLoginAccountId;
        ReviewedAtUtc = reviewedAtUtc;
        DeadlineAtUtc = null;
        return Result.Success();
    }

    public Result Reject(long reviewedByLoginAccountId, int rejectionGracePeriodDays, DateTime reviewedAtUtc)
    {
        if (reviewedByLoginAccountId <= 0 ||
            rejectionGracePeriodDays <= 0 ||
            StatusCode != DeferExtensionRequestStatusCodes.Pending)
        {
            return Result.Failure(BillingErrors.InvalidDeferExtensionRequest);
        }

        StatusCode = DeferExtensionRequestStatusCodes.Rejected;
        ReviewedByLoginAccountId = reviewedByLoginAccountId;
        ReviewedAtUtc = reviewedAtUtc;
        DeadlineAtUtc = reviewedAtUtc.AddDays(rejectionGracePeriodDays);
        return Result.Success();
    }
}

public static class DeferExtensionRequestStatusCodes
{
    public const string Pending = "PENDING";
    public const string Approved = "APPROVED";
    public const string Rejected = "REJECTED";
}
