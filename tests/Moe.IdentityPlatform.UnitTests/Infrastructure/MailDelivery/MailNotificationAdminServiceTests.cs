using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Persistence;
using Moe.Modules.MailDelivery;
using Moe.Modules.MailDelivery.Application.Admin;
using Moe.Modules.MailDelivery.Domain;
using Moe.SharedKernel.Results;
using Moe.StudentFinance.Persistence;
using Xunit;

namespace Moe.IdentityPlatform.UnitTests.Infrastructure.MailDelivery;

public sealed class MailNotificationAdminServiceTests
{
    private readonly TestClock _clock = new(new DateTimeOffset(2026, 7, 1, 4, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task ListAsync_Returns_Filtered_Page()
    {
        using MoeDbContext dbContext = CreateDbContext();
        await AddNotificationAsync(dbContext, personId: 1001, status: EmailNotificationStatusCodes.Pending);
        await AddNotificationAsync(dbContext, personId: 1002, status: EmailNotificationStatusCodes.FailedFinal);
        MailNotificationAdminService service = CreateService(dbContext);

        var page = await service.ListAsync(
            new MailNotificationFilter(null, EmailNotificationStatusCodes.FailedFinal, null, null, null, null, null, null, null, null),
            page: 1,
            pageSize: 25,
            CancellationToken.None);

        page.TotalCount.Should().Be(1);
        page.Items.Should().ContainSingle(x => x.PersonId == 1002);
    }

    [Fact]
    public async Task GetSummaryAsync_Returns_Status_Counts()
    {
        using MoeDbContext dbContext = CreateDbContext();
        await AddNotificationAsync(dbContext, personId: 1001, status: EmailNotificationStatusCodes.Pending);
        await AddNotificationAsync(dbContext, personId: 1002, status: EmailNotificationStatusCodes.FailedRetryable);
        await AddNotificationAsync(dbContext, personId: 1003, status: EmailNotificationStatusCodes.Sent);
        MailNotificationAdminService service = CreateService(dbContext);

        MailNotificationSummary summary = await service.GetSummaryAsync(CancellationToken.None);

        summary.Pending.Should().Be(1);
        summary.FailedRetryable.Should().Be(1);
        summary.SentToday.Should().Be(1);
    }

    [Fact]
    public async Task RetryAsync_WhenFailed_ResetsToPending()
    {
        using MoeDbContext dbContext = CreateDbContext();
        EmailNotification notification = await AddNotificationAsync(
            dbContext,
            personId: 1001,
            status: EmailNotificationStatusCodes.FailedFinal);
        MailNotificationAdminService service = CreateService(dbContext);

        Result<MailNotificationDetail> result = await service.RetryAsync(notification.Id, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.StatusCode.Should().Be(EmailNotificationStatusCodes.Pending);
        result.Value.AttemptCount.Should().Be(0);
    }

    [Fact]
    public async Task CancelAsync_WhenPending_MarksCancelled()
    {
        using MoeDbContext dbContext = CreateDbContext();
        EmailNotification notification = await AddNotificationAsync(
            dbContext,
            personId: 1001,
            status: EmailNotificationStatusCodes.Pending);
        MailNotificationAdminService service = CreateService(dbContext);

        Result<MailNotificationDetail> result = await service.CancelAsync(notification.Id, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.StatusCode.Should().Be(EmailNotificationStatusCodes.Cancelled);
        result.Value.CancelledAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task SuppressAsync_WhenFinalFailed_MarksSuppressed()
    {
        using MoeDbContext dbContext = CreateDbContext();
        EmailNotification notification = await AddNotificationAsync(
            dbContext,
            personId: 1001,
            status: EmailNotificationStatusCodes.FailedFinal);
        MailNotificationAdminService service = CreateService(dbContext);

        Result<MailNotificationDetail> result = await service.SuppressAsync(
            notification.Id,
            "Handled manually.",
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.StatusCode.Should().Be(EmailNotificationStatusCodes.Suppressed);
        result.Value.LastErrorMessage.Should().Be("Handled manually.");
    }

    [Fact]
    public async Task ListAsync_Applies_AccessScope()
    {
        using MoeDbContext dbContext = CreateDbContext();
        await AddNotificationAsync(dbContext, personId: 1001, status: EmailNotificationStatusCodes.Pending);
        await AddNotificationAsync(dbContext, personId: 2001, status: EmailNotificationStatusCodes.Pending);
        MailNotificationAdminService service = CreateService(
            dbContext,
            new PersonIdMailNotificationAccessScope(1001));

        var page = await service.ListAsync(
            new MailNotificationFilter(null, null, null, null, null, null, null, null, null, null),
            page: 1,
            pageSize: 25,
            CancellationToken.None);

        page.TotalCount.Should().Be(1);
        page.Items.Should().ContainSingle(x => x.PersonId == 1001);
    }

    [Fact]
    public async Task GetSummaryAsync_Applies_AccessScope()
    {
        using MoeDbContext dbContext = CreateDbContext();
        await AddNotificationAsync(dbContext, personId: 1001, status: EmailNotificationStatusCodes.Pending);
        await AddNotificationAsync(dbContext, personId: 2001, status: EmailNotificationStatusCodes.FailedFinal);
        MailNotificationAdminService service = CreateService(
            dbContext,
            new PersonIdMailNotificationAccessScope(1001));

        MailNotificationSummary summary = await service.GetSummaryAsync(CancellationToken.None);

        summary.Pending.Should().Be(1);
        summary.FailedFinal.Should().Be(0);
    }

    [Fact]
    public async Task RetryAsync_WhenOutsideAccessScope_ReturnsNotFound()
    {
        using MoeDbContext dbContext = CreateDbContext();
        EmailNotification notification = await AddNotificationAsync(
            dbContext,
            personId: 2001,
            status: EmailNotificationStatusCodes.FailedFinal);
        MailNotificationAdminService service = CreateService(
            dbContext,
            new PersonIdMailNotificationAccessScope(1001));

        Result<MailNotificationDetail> result = await service.RetryAsync(notification.Id, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(MailDeliveryErrors.NotificationNotFound);
    }

    [Fact]
    public async Task ListAsync_Applies_ServerSearch_And_ServerSort()
    {
        using MoeDbContext dbContext = CreateDbContext();
        await AddNotificationAsync(dbContext, personId: 1001, status: EmailNotificationStatusCodes.Pending, subject: "Alpha notice");
        await AddNotificationAsync(dbContext, personId: 1003, status: EmailNotificationStatusCodes.Pending, subject: "Alpha reminder");
        await AddNotificationAsync(dbContext, personId: 1002, status: EmailNotificationStatusCodes.Pending, subject: "Beta notice");
        MailNotificationAdminService service = CreateService(dbContext);

        var page = await service.ListAsync(
            new MailNotificationFilter("Alpha", null, null, null, null, null, null, null, "personId", "desc"),
            page: 1,
            pageSize: 25,
            CancellationToken.None);

        page.TotalCount.Should().Be(2);
        page.Items.Select(x => x.PersonId).Should().Equal(1003, 1001);
    }

    private static MoeDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MoeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new MoeDbContext(options, [new MailDeliveryModelConfiguration()]);
    }

    private MailNotificationAdminService CreateService(MoeDbContext dbContext)
        => new(dbContext, _clock, new AllowAllMailNotificationAccessScope());

    private MailNotificationAdminService CreateService(
        MoeDbContext dbContext,
        IMailNotificationAccessScope accessScope)
        => new(dbContext, _clock, accessScope);

    private static async Task<EmailNotification> AddNotificationAsync(
        MoeDbContext dbContext,
        long personId,
        string status,
        string subject = "Test subject")
    {
        DateTime createdAtUtc = new(2026, 7, 1, 4, 0, 0, DateTimeKind.Utc);
        EmailNotification notification = EmailNotification.Create(
            "NOTI-TEST",
            personId,
            subject,
            "Test body",
            "<p>Test body</p>",
            "TestEntity",
            personId.ToString(),
            createdAtUtc,
            maxAttempts: 3);

        switch (status)
        {
            case EmailNotificationStatusCodes.Pending:
                break;
            case EmailNotificationStatusCodes.FailedRetryable:
                notification.MarkRetryableFailure("MAIL.TEST", "Retryable failure.", createdAtUtc.AddMinutes(5));
                break;
            case EmailNotificationStatusCodes.FailedFinal:
                notification.MarkFinalFailure("MAIL.TEST", "Final failure.", createdAtUtc);
                break;
            case EmailNotificationStatusCodes.Sent:
                notification.MarkSent("s***@example.com", "CONTACT", createdAtUtc);
                break;
        }

        dbContext.Set<EmailNotification>().Add(notification);
        await dbContext.SaveChangesAsync();
        return notification;
    }

    private sealed class TestClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;

        public DateOnly TodayInSingapore() => SingaporeBusinessDay.FromUtc(UtcNow);
    }

    private sealed class PersonIdMailNotificationAccessScope(params long[] personIds) : IMailNotificationAccessScope
    {
        public IQueryable<EmailNotification> Apply(IQueryable<EmailNotification> query)
            => query.Where(notification => personIds.Contains(notification.PersonId));
    }
}
