namespace Moe.Modules.FasPayment.Domain.Payments;

internal static class CoursePaymentPlanPolicy
{
    public static readonly int[] AllowedInstallmentCounts = [2, 3, 6, 9, 12];
    public const int DefaultInstallmentCount = 3;

    public static bool IsAllowedInstallmentCount(int installmentCount)
        => AllowedInstallmentCounts.Contains(installmentCount);
}
