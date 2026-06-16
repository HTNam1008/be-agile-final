namespace Moe.Modules.IdentityPlatform.Domain.Iam;

internal static class ProvisioningStatusCodes
{
    public const string Pending = "PENDING";
    public const string Completed = "COMPLETED";
    public const string FailedManualReview = "FAILED_MANUAL_REVIEW";
    public const string Cancelled = "CANCELLED";
}
