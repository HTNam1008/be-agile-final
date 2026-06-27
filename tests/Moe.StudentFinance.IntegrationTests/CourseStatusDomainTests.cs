using Moe.Modules.CourseBilling.Domain.Courses;
using Xunit;

namespace Moe.StudentFinance.IntegrationTests;

public sealed class CourseStatusDomainTests
{
    [Fact]
    public void Enable_Rejects_Draft_Course()
    {
        Course course = CreateCourse();

        var exception = Assert.Throws<InvalidOperationException>(
            () => course.Enable(11, new DateTime(2026, 6, 2, 0, 0, 0, DateTimeKind.Utc)));

        Assert.Equal(CourseErrors.CourseNotDisabled.Message, exception.Message);
        Assert.Equal(CourseStatusCodes.Draft, course.CourseStatusCode);
        Assert.Null(course.DisabledAtUtc);
        Assert.Null(course.DisabledByLoginAccountId);
    }

    [Fact]
    public void Enable_Rejects_Published_Course()
    {
        Course course = CreateCourse();
        course.Publish(10, new DateTime(2026, 6, 1, 1, 0, 0, DateTimeKind.Utc));

        var exception = Assert.Throws<InvalidOperationException>(
            () => course.Enable(11, new DateTime(2026, 6, 2, 0, 0, 0, DateTimeKind.Utc)));

        Assert.Equal(CourseErrors.CourseNotDisabled.Message, exception.Message);
        Assert.Equal(CourseStatusCodes.Published, course.CourseStatusCode);
    }

    [Fact]
    public void Enable_Publishes_Disabled_Course_And_Clears_Disabled_Metadata()
    {
        Course course = CreateCourse();
        DateTime disabledAt = new(2026, 6, 2, 0, 0, 0, DateTimeKind.Utc);
        DateTime enabledAt = new(2026, 6, 3, 0, 0, 0, DateTimeKind.Utc);
        course.Disable(10, disabledAt);

        course.Enable(11, enabledAt);

        Assert.Equal(CourseStatusCodes.Published, course.CourseStatusCode);
        Assert.Null(course.DisabledAtUtc);
        Assert.Null(course.DisabledByLoginAccountId);
        Assert.Equal(11, course.UpdatedByLoginAccountId);
        Assert.Equal(enabledAt, course.UpdatedAtUtc);
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
}
