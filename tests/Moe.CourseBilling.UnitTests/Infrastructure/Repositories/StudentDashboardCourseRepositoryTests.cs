using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moe.Modules.CourseBilling;
using Moe.Modules.CourseBilling.Domain.Courses;
using Moe.Modules.CourseBilling.Infrastructure.Repositories;
using Moe.StudentFinance.Persistence;
using Xunit;

namespace Moe.CourseBilling.UnitTests.Infrastructure.Repositories;

public sealed class StudentDashboardCourseRepositoryTests : IAsyncLifetime
{
    private readonly MoeDbContext _dbContext;
    private readonly StudentDashboardCourseRepository _repository;

    public StudentDashboardCourseRepositoryTests()
    {
        DbContextOptions<MoeDbContext> options = new DbContextOptionsBuilder<MoeDbContext>()
            .UseInMemoryDatabase($"student-dashboard-courses-{Guid.NewGuid():N}")
            .Options;

        _dbContext = new MoeDbContext(options, [new CourseBillingModelConfiguration()]);
        _repository = new StudentDashboardCourseRepository(_dbContext);
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
    public async Task ListCurrentCoursesAsync_ReturnsCalculatedTotalFeeWithPercentageGst()
    {
        Course course = new(
            organizationId: 10,
            courseCode: "GST-COURSE",
            courseName: "GST Course",
            description: null,
            startDate: new DateOnly(2026, 7, 1),
            endDate: new DateOnly(2026, 8, 1),
            enrollmentOpenAtUtc: new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            enrollmentCloseAtUtc: new DateTime(2026, 6, 30, 16, 0, 0, DateTimeKind.Utc),
            actorLoginAccountId: 42,
            utcNow: new DateTime(2026, 6, 1, 8, 0, 0, DateTimeKind.Utc));
        FeeComponent tuition = new("TUITION", "Tuition", "BASE", FeeComponentCalculationTypes.Fixed, false, 0m, false, true);
        FeeComponent gst = new("GST", "GST", "TAX", FeeComponentCalculationTypes.Percentage, true, 9m, true, true);
        _dbContext.AddRange(course, tuition, gst);
        await _dbContext.SaveChangesAsync();

        _dbContext.AddRange(
            new CourseFee(course.Id, tuition.Id, 1000m, 1),
            new CourseFee(course.Id, gst.Id, 9m, 999),
            CourseEnrollment.EnrollByAdmin(
                personId: 5001,
                courseId: course.Id,
                coursePaymentPlanId: 100,
                adminLoginAccountId: 42,
                enrolledAtUtc: new DateTime(2026, 6, 15, 8, 0, 0, DateTimeKind.Utc),
                beforeStartRefundPercentage: CourseRefundPolicyDefaults.BeforeStartPercentage,
                afterStartRefundPercentage: CourseRefundPolicyDefaults.AfterStartPercentage).Value);
        await _dbContext.SaveChangesAsync();

        var courses = await _repository.ListCurrentCoursesAsync(5001, search: null, status: null, CancellationToken.None);

        courses.Should().ContainSingle();
        courses.Single().HasActiveFee.Should().BeTrue();
        courses.Single().TotalFee.Should().Be(1090m);
    }
}
