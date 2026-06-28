namespace Moe.Modules.CourseBilling.Contracts.BillingConfiguration;

public sealed record BillingConfigurationResponse(
    long OrganizationId,
    int MaxDeferralCount,
    int RejectionGracePeriodDays,
    long UpdatedByLoginAccountId,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record UpdateBillingConfigurationRequest(
    long? OrganizationId,
    int MaxDeferralCount,
    int RejectionGracePeriodDays);
