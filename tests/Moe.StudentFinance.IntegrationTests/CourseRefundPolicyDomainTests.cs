using Moe.Modules.CourseBilling.Application.Enrollments.CourseContent;
using Moe.Modules.CourseBilling.Domain.Courses;
using Xunit;

namespace Moe.StudentFinance.IntegrationTests;

public sealed class CourseRefundPolicyDomainTests
{
    [Fact]
    public void Course_Uses_Demo_Refund_Defaults()
    {
        Course course = CreateCourse();

        Assert.Equal(100m, course.BeforeStartRefundPercentage);
        Assert.Equal(50m, course.AfterStartRefundPercentage);
    }

    [Fact]
    public void Enrollment_Snapshots_Course_Refund_Policy()
    {
        Course course = CreateCourse();
        course.UpdateRefundPolicy(80m, 25m, 10, DateTime.UtcNow);

        CourseEnrollment enrollment = CourseEnrollment.JoinSelf(
            1,
            2,
            3,
            4,
            DateTime.UtcNow,
            course.BeforeStartRefundPercentage,
            course.AfterStartRefundPercentage).Value;

        course.UpdateRefundPolicy(10m, 0m, 10, DateTime.UtcNow);

        Assert.Equal(80m, enrollment.BeforeStartRefundPercentage);
        Assert.Equal(25m, enrollment.AfterStartRefundPercentage);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(100.01)]
    public void Course_Rejects_Invalid_Refund_Percentage(decimal invalidPercentage)
    {
        Course course = CreateCourse();

        var result = course.UpdateRefundPolicy(invalidPercentage, 50m, 10, DateTime.UtcNow);

        Assert.True(result.IsFailure);
        Assert.Equal(CourseErrors.InvalidRefundPercentage, result.Error);
    }

    [Fact]
    public void Course_Content_Remains_Locked_Until_Start_Date()
    {
        Course course = CreateCourse();
        CourseEnrollment enrollment = CreateActiveEnrollment(course);

        var result = CourseContentAccessPolicy.Check(enrollment, course, new DateOnly(2026, 7, 31));

        Assert.True(result.IsFailure);
        Assert.Equal(CourseBillingErrors.CourseContentNotOpen, result.Error);
    }

    [Fact]
    public void Course_Content_Opens_On_Start_Date_For_Active_Enrollment()
    {
        Course course = CreateCourse();
        CourseEnrollment enrollment = CreateActiveEnrollment(course);

        var result = CourseContentAccessPolicy.Check(enrollment, course, course.StartDate);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Course_Content_Stays_Locked_For_Pending_Payment()
    {
        Course course = CreateCourse();
        CourseEnrollment enrollment = CourseEnrollment.JoinSelf(
            1, 2, 3, 4, DateTime.UtcNow, 100m, 50m).Value;

        var result = CourseContentAccessPolicy.Check(enrollment, course, course.StartDate);

        Assert.True(result.IsFailure);
        Assert.Equal(CourseBillingErrors.CourseContentLocked, result.Error);
    }

    private static Course CreateCourse()
        => new(
            1,
            "CS101",
            "Computer Science",
            null,
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 10, 1),
            new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 7, 31, 0, 0, 0, DateTimeKind.Utc),
            10,
            new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));

    private static CourseEnrollment CreateActiveEnrollment(Course course)
    {
        CourseEnrollment enrollment = CourseEnrollment.JoinSelf(
            1, course.Id == 0 ? 2 : course.Id, 3, 4, DateTime.UtcNow, 100m, 50m).Value;
        enrollment.ActivateInstallmentEnrollment();
        return enrollment;
    }
}
