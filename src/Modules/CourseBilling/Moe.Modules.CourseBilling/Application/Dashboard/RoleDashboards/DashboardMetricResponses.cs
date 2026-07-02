namespace Moe.Modules.CourseBilling.Application.Dashboard.RoleDashboards;

public sealed record DashboardCountMetricResponse(long Value, decimal? ChangePercent);

public sealed record DashboardAmountMetricResponse(
    decimal Value,
    string CurrencyCode,
    decimal? ChangePercent);

internal static class DashboardTrend
{
    public static decimal? Calculate(long currentValue, long previousValue)
        => Calculate((decimal)currentValue, previousValue);

    public static decimal? Calculate(decimal currentValue, decimal previousValue)
    {
        if (previousValue == 0m)
        {
            return currentValue == 0m ? 0m : null;
        }

        return decimal.Round(
            (currentValue - previousValue) / previousValue * 100m,
            1,
            MidpointRounding.AwayFromZero);
    }
}
