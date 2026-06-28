namespace Moe.Modules.CourseBilling.Domain.Billing;

internal static class BillDeferralPolicy
{
    public const int DefaultMaxDeferralCount = 2;
    public const int DefaultRejectionGracePeriodDays = 7;
}
