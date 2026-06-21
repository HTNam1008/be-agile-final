using Moe.Modules.EducationAccountTopUp.Contracts.TopUps.Enums;

namespace Moe.Modules.EducationAccountTopUp.Domain.TopUps;

public static class RecurrenceCalculator
{
    public static DateTime? CalculateNextRun(
        string frequencyCode,
        int interval,
        DateTime lastRunDate,
        DateOnly? endDate)
    {
        if (interval <= 0) return null;

        DateTime nextRun = lastRunDate;

        if (string.Equals(frequencyCode, FrequencyCode.Daily.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            nextRun = lastRunDate.AddDays(interval);
        }
        else if (string.Equals(frequencyCode, FrequencyCode.Weekly.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            nextRun = lastRunDate.AddDays(interval * 7);
        }
        else if (string.Equals(frequencyCode, FrequencyCode.Monthly.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            nextRun = lastRunDate.AddMonths(interval);
        }
        else if (string.Equals(frequencyCode, FrequencyCode.Yearly.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            nextRun = lastRunDate.AddYears(interval);
        }
        else
        {
            return null; // Unknown frequency
        }

        if (endDate.HasValue)
        {
            DateTime endDateTime = endDate.Value.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);
            if (nextRun > endDateTime)
            {
                return null;
            }
        }

        return nextRun;
    }
}
