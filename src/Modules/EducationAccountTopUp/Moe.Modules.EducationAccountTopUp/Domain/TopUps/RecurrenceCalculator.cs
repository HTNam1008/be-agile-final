using Moe.Modules.EducationAccountTopUp.Contracts.TopUps.Enums;

namespace Moe.Modules.EducationAccountTopUp.Domain.TopUps;

public static class RecurrenceCalculator
{
    public static DateTime? CalculateNextRun(
        string frequencyCode,
        int interval,
        DateTime lastRunDate,
        DateOnly? endDate,
        int? weeklyDayOfWeek = null,
        int? monthlyDay = null)
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
            if (weeklyDayOfWeek.HasValue)
            {
                int target = weeklyDayOfWeek.Value;
                int delta = (target - (int)nextRun.DayOfWeek + 7) % 7;
                nextRun = nextRun.AddDays(delta);
            }
        }
        else if (string.Equals(frequencyCode, FrequencyCode.Monthly.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            nextRun = lastRunDate.AddMonths(interval);
            if (monthlyDay.HasValue)
            {
                int day = Math.Min(monthlyDay.Value, DateTime.DaysInMonth(nextRun.Year, nextRun.Month));
                nextRun = new DateTime(
                    nextRun.Year,
                    nextRun.Month,
                    day,
                    lastRunDate.Hour,
                    lastRunDate.Minute,
                    lastRunDate.Second,
                    lastRunDate.Kind);
            }
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
