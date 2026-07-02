namespace Moe.Modules.CourseBilling.Application.Enrollments.SelfJoinCourse;

internal static class InstallmentBillingSchedule
{
    public static DateOnly FirstDueDateForNextMonthlyStatement(DateTime utcNow)
        => new DateOnly(utcNow.Year, utcNow.Month, 8).AddMonths(1);
}
