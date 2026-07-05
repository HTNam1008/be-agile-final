using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moe.Modules.CourseBilling;
using Moe.Modules.CourseBilling.Contracts.AdminCourses;
using Moe.Modules.CourseBilling.Domain.Courses;
using Moe.Modules.CourseBilling.Infrastructure.Repositories;
using Moe.Modules.IdentityPlatform.IGateway.Students;
using Moe.Modules.Notifications.IGateway.Notifications;
using Moe.SharedKernel.Results;
using Moe.StudentFinance.Persistence;
using Xunit;

namespace Moe.CourseBilling.UnitTests.Infrastructure.Repositories;

public sealed class AdminCourseRepositoryTests
{
    [Fact]
    public async Task ListCoursesAsync_CanSortByTotalFeeUsingRelationalProvider()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        DbContextOptions<MoeDbContext> options = new DbContextOptionsBuilder<MoeDbContext>()
            .UseSqlite(connection)
            .Options;

        await using MoeDbContext dbContext = new(options, [new CourseBillingModelConfiguration()]);
        await dbContext.Database.EnsureCreatedAsync();

        FeeComponent tuition = new("TUITION", "Tuition", "BASE", FeeComponentCalculationTypes.Fixed, false, 0m, false, true);
        FeeComponent gst = new("GST", "GST", "TAX", FeeComponentCalculationTypes.Percentage, true, 9m, true, true);
        Course lowerFeeCourse = CreateCourse("LOW", "Lower fee course");
        Course higherFeeCourse = CreateCourse("HIGH", "Higher fee course");
        dbContext.AddRange(tuition, gst, lowerFeeCourse, higherFeeCourse);
        await dbContext.SaveChangesAsync();

        dbContext.AddRange(
            new CourseFee(lowerFeeCourse.Id, tuition.Id, 100m, 1),
            new CourseFee(lowerFeeCourse.Id, gst.Id, 9m, 999),
            new CourseFee(higherFeeCourse.Id, tuition.Id, 200m, 1),
            new CourseFee(higherFeeCourse.Id, gst.Id, 9m, 999));
        await dbContext.SaveChangesAsync();

        var repository = new AdminCourseRepository(
            dbContext,
            new StudentNotificationRecipientResolverDouble(),
            new SchoolAdminNotificationRecipientResolverDouble(),
            new NotificationWriterDouble(),
            NullLogger<AdminCourseRepository>.Instance);

        var result = await repository.ListCoursesAsync(
            new CourseQueryRequest(OrganizationId: 10, SortBy: "totalFee", SortDirection: "desc"),
            scopedOrganizationIds: [10],
            CancellationToken.None);

        result.Items.Should().HaveCount(2);
        result.Items.Select(x => x.CourseCode).Should().Equal("HIGH", "LOW");
        result.Items.Select(x => x.TotalFeeAmount).Should().Equal(218m, 109m);
    }

    private static Course CreateCourse(string code, string name)
        => new(
            organizationId: 10,
            courseCode: code,
            courseName: name,
            description: null,
            startDate: new DateOnly(2026, 7, 1),
            endDate: new DateOnly(2026, 8, 1),
            enrollmentOpenAtUtc: new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            enrollmentCloseAtUtc: new DateTime(2026, 6, 30, 16, 0, 0, DateTimeKind.Utc),
            actorLoginAccountId: 42,
            utcNow: new DateTime(2026, 6, 1, 8, 0, 0, DateTimeKind.Utc));

    private sealed class StudentNotificationRecipientResolverDouble : IStudentNotificationRecipientResolver
    {
        public Task<long?> FindUserAccountIdByPersonIdAsync(long personId, CancellationToken cancellationToken)
            => Task.FromResult<long?>(null);
    }

    private sealed class SchoolAdminNotificationRecipientResolverDouble : ISchoolAdminNotificationRecipientResolver
    {
        public Task<IReadOnlyCollection<long>> FindUserAccountIdsByOrganizationIdAsync(long organizationId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<long>>(Array.Empty<long>());
    }

    private sealed class NotificationWriterDouble : INotificationWriter
    {
        public Task<Result<long>> CreateAsync(NotificationCreateRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(Result<long>.Success(1));
    }
}
