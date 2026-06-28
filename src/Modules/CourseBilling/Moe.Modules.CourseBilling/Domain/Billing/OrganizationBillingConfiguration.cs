using Moe.SharedKernel.Domain;
using Moe.SharedKernel.Results;

namespace Moe.Modules.CourseBilling.Domain.Billing;

internal sealed class OrganizationBillingConfiguration : Entity<long>
{
    private OrganizationBillingConfiguration() : base(0) { }

    private OrganizationBillingConfiguration(
        long organizationId,
        int maxDeferralCount,
        int rejectionGracePeriodDays,
        long updatedByLoginAccountId,
        DateTime updatedAtUtc) : base(0)
    {
        OrganizationId = organizationId;
        MaxDeferralCount = maxDeferralCount;
        RejectionGracePeriodDays = rejectionGracePeriodDays;
        UpdatedByLoginAccountId = updatedByLoginAccountId;
        UpdatedAtUtc = updatedAtUtc;
        CreatedAtUtc = updatedAtUtc;
    }

    public long OrganizationId { get; private set; }
    public int MaxDeferralCount { get; private set; }
    public int RejectionGracePeriodDays { get; private set; }
    public long UpdatedByLoginAccountId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public static Result<OrganizationBillingConfiguration> CreateDefault(
        long organizationId,
        long updatedByLoginAccountId,
        DateTime updatedAtUtc)
        => Create(
            organizationId,
            BillDeferralPolicy.DefaultMaxDeferralCount,
            BillDeferralPolicy.DefaultRejectionGracePeriodDays,
            updatedByLoginAccountId,
            updatedAtUtc);

    public static Result<OrganizationBillingConfiguration> Create(
        long organizationId,
        int maxDeferralCount,
        int rejectionGracePeriodDays,
        long updatedByLoginAccountId,
        DateTime updatedAtUtc)
    {
        Result validation = Validate(organizationId, maxDeferralCount, rejectionGracePeriodDays, updatedByLoginAccountId);
        if (validation.IsFailure)
        {
            return Result<OrganizationBillingConfiguration>.Failure(validation.Error);
        }

        return Result<OrganizationBillingConfiguration>.Success(new(
            organizationId,
            maxDeferralCount,
            rejectionGracePeriodDays,
            updatedByLoginAccountId,
            updatedAtUtc));
    }

    public Result Update(
        int maxDeferralCount,
        int rejectionGracePeriodDays,
        long updatedByLoginAccountId,
        DateTime updatedAtUtc)
    {
        Result validation = Validate(OrganizationId, maxDeferralCount, rejectionGracePeriodDays, updatedByLoginAccountId);
        if (validation.IsFailure)
        {
            return validation;
        }

        MaxDeferralCount = maxDeferralCount;
        RejectionGracePeriodDays = rejectionGracePeriodDays;
        UpdatedByLoginAccountId = updatedByLoginAccountId;
        UpdatedAtUtc = updatedAtUtc;
        return Result.Success();
    }

    private static Result Validate(
        long organizationId,
        int maxDeferralCount,
        int rejectionGracePeriodDays,
        long updatedByLoginAccountId)
    {
        if (organizationId <= 0 || updatedByLoginAccountId <= 0)
        {
            return Result.Failure(BillingErrors.InvalidBillingConfiguration);
        }

        if (maxDeferralCount is < 0 or > 12 || rejectionGracePeriodDays is < 1 or > 90)
        {
            return Result.Failure(BillingErrors.InvalidBillingConfiguration);
        }

        return Result.Success();
    }
}
