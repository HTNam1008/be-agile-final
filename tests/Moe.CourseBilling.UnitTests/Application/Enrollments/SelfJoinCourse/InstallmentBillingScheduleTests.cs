using FluentAssertions;
using Moe.Modules.CourseBilling.Application.Enrollments.SelfJoinCourse;
using Xunit;

namespace Moe.CourseBilling.UnitTests.Application.Enrollments.SelfJoinCourse;

public sealed class InstallmentBillingScheduleTests
{
    [Fact]
    public void FirstDueDateForNextMonthlyStatement_ReturnsEighthDayOfNextMonth()
    {
        DateOnly dueDate = InstallmentBillingSchedule.FirstDueDateForNextMonthlyStatement(
            new DateTime(2026, 7, 1, 4, 30, 0, DateTimeKind.Utc));

        dueDate.Should().Be(new DateOnly(2026, 8, 8));
    }
}
