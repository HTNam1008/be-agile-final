namespace Moe.Modules.CourseBilling.Infrastructure;

internal sealed class CourseBillingWorkerOptions
{
    public const string SectionName = "CourseBillingWorker";

    public int MonthlyBillNotificationPollIntervalSeconds { get; init; } = 3600;

    public int MissedInstallmentPaymentEmailPollIntervalSeconds { get; init; } = 86400;
}
