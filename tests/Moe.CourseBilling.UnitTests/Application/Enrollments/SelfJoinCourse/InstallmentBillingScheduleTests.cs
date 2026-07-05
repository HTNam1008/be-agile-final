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
            new DateOnly(2026, 7, 1));

        dueDate.Should().Be(new DateOnly(2026, 8, 8));
    }

    [Fact]
    public void FirstDueDateForNextMonthlyStatement_UsesSingaporeBusinessMonth()
    {
        DateOnly dueDate = InstallmentBillingSchedule.FirstDueDateForNextMonthlyStatement(
            new DateOnly(2026, 8, 1));

        dueDate.Should().Be(new DateOnly(2026, 9, 8));
    }
}
