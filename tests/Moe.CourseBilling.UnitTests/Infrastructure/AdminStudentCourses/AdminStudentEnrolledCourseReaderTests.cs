using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moe.Modules.CourseBilling;
using Moe.Modules.CourseBilling.Domain.Billing;
using Moe.Modules.CourseBilling.Domain.Courses;
using Moe.Modules.CourseBilling.Infrastructure.AdminStudentCourses;
using Moe.StudentFinance.Persistence;
using Xunit;

namespace Moe.CourseBilling.UnitTests.Infrastructure.AdminStudentCourses;

public sealed class AdminStudentEnrolledCourseReaderTests : IAsyncLifetime
{
    private readonly MoeDbContext _dbContext;
    private readonly AdminStudentEnrolledCourseReader _reader;

    public AdminStudentEnrolledCourseReaderTests()
    {
        DbContextOptions<MoeDbContext> options = new DbContextOptionsBuilder<MoeDbContext>()
            .UseInMemoryDatabase($"admin-student-courses-{Guid.NewGuid():N}")
            .Options;

        _dbContext = new MoeDbContext(options, [new CourseBillingModelConfiguration()]);
        _reader = new AdminStudentEnrolledCourseReader(_dbContext);
    }

    public async Task InitializeAsync()
    {
        await _dbContext.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _dbContext.DisposeAsync();
    }

    [Fact]
    public async Task ListAsync_ReturnsNewestEnrollmentsWithBillFieldsAndKeepsNoBillRows()
    {
        Course olderCourse = CreateCourse("COURSE-OLD", "Older Course");
        Course middleCourse = CreateCourse("COURSE-MID", "Middle Course");
        Course newerCourse = CreateCourse("COURSE-NEW", "Newer Course");
        _dbContext.AddRange(olderCourse, middleCourse, newerCourse);
        await _dbContext.SaveChangesAsync();

        CourseEnrollment olderEnrollment = CreateEnrollment(5001, olderCourse.Id, new DateTime(2026, 6, 1, 8, 0, 0, DateTimeKind.Utc));
        CourseEnrollment middleEnrollment = CreateEnrollment(5001, middleCourse.Id, new DateTime(2026, 6, 5, 8, 0, 0, DateTimeKind.Utc));
        CourseEnrollment newerEnrollment = CreateEnrollment(5001, newerCourse.Id, new DateTime(2026, 6, 10, 8, 0, 0, DateTimeKind.Utc));
        CourseEnrollment otherStudentEnrollment = CreateEnrollment(6001, newerCourse.Id, new DateTime(2026, 6, 11, 8, 0, 0, DateTimeKind.Utc));
        _dbContext.AddRange(olderEnrollment, middleEnrollment, newerEnrollment, otherStudentEnrollment);
        await _dbContext.SaveChangesAsync();

        _dbContext.Add(Bill.IssueForCourseEnrollment(
            middleEnrollment.Id,
            "BILL-MID",
            new DateTime(2026, 6, 5, 9, 0, 0, DateTimeKind.Utc),
            new DateOnly(2026, 7, 5),
            grossAmount: 120m,
            subsidyAmount: 20m).Value);
        await _dbContext.SaveChangesAsync();

        var page = await _reader.ListAsync(5001, page: 2, pageSize: 1, CancellationToken.None);

        page.TotalCount.Should().Be(3);
        page.Items.Should().ContainSingle();
        page.Items[0].CourseId.Should().Be(middleCourse.Id);
        page.Items[0].CourseName.Should().Be("Middle Course");
        page.Items[0].Fee.Should().Be(120m);
        page.Items[0].FasApplied.Should().Be(20m);
        page.Items[0].Paid.Should().Be(0m);
        page.Items[0].Outstanding.Should().Be(100m);
    }

    [Fact]
    public async Task ListAsync_OnEnrollmentWithoutBill_ReturnsZeroMoneyFields()
    {
        Course course = CreateCourse("COURSE-FREE", "Free Course");
        _dbContext.Add(course);
        await _dbContext.SaveChangesAsync();

        CourseEnrollment enrollment = CreateEnrollment(5002, course.Id, new DateTime(2026, 6, 12, 8, 0, 0, DateTimeKind.Utc));
        _dbContext.Add(enrollment);
        await _dbContext.SaveChangesAsync();

        var page = await _reader.ListAsync(5002, page: 1, pageSize: 20, CancellationToken.None);

        page.TotalCount.Should().Be(1);
        var item = page.Items.Should().ContainSingle().Subject;
        item.Fee.Should().Be(0m);
        item.FasApplied.Should().Be(0m);
        item.Paid.Should().Be(0m);
        item.Outstanding.Should().Be(0m);
    }

    [Fact]
    public async Task ListAsync_OnStudentWithNoEnrollments_ReturnsEmptyPage()
    {
        var page = await _reader.ListAsync(9999, page: 1, pageSize: 20, CancellationToken.None);

        page.TotalCount.Should().Be(0);
        page.Items.Should().BeEmpty();
    }

    private static Course CreateCourse(string courseCode, string courseName)
        => new(
            organizationId: 10,
            courseCode,
            courseName,
            description: null,
            startDate: new DateOnly(2026, 7, 1),
            endDate: new DateOnly(2026, 8, 1),
            enrollmentOpenAtUtc: new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            enrollmentCloseAtUtc: new DateTime(2026, 6, 30, 16, 0, 0, DateTimeKind.Utc),
            actorLoginAccountId: 42,
            utcNow: new DateTime(2026, 6, 1, 8, 0, 0, DateTimeKind.Utc));

    private static CourseEnrollment CreateEnrollment(long personId, long courseId, DateTime enrolledAtUtc)
    {
        var enrollment = CourseEnrollment.EnrollByAdminPendingPlanSelection(
            personId: personId,
            courseId: courseId,
            adminLoginAccountId: 42,
            enrolledAtUtc: enrolledAtUtc,
            beforeStartRefundPercentage: CourseRefundPolicyDefaults.BeforeStartPercentage,
            afterStartRefundPercentage: CourseRefundPolicyDefaults.AfterStartPercentage).Value;
        enrollment.ChangePaymentPlan(coursePaymentPlanId: 100, installment: false);
        return enrollment;
    }
}
