namespace Moe.Modules.CourseBilling.Application.Enrollments.SelfJoinCourse;

internal static class InstallmentBillingSchedule
{
    public static DateOnly FirstDueDateForNextMonthlyStatement(DateOnly singaporeToday)
        => new DateOnly(singaporeToday.Year, singaporeToday.Month, 8).AddMonths(1);
}
