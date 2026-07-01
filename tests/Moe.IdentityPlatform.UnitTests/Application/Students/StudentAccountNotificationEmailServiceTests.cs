using FluentAssertions;
using Moe.Modules.IdentityPlatform.Application.Students;
using Moe.Modules.MailDelivery.IGateway;
using Xunit;

namespace Moe.IdentityPlatform.UnitTests.Application.Students;

public sealed class StudentAccountNotificationEmailServiceTests
{
    [Fact]
    public async Task SendStudentAccountCreatedAsync_EnqueuesPortalAccessReadyMail()
    {
        RecordingEmailNotificationScheduler scheduler = new();
        StudentAccountNotificationEmailService service = new(scheduler, new TestEmailBrandingProvider());

        await service.SendStudentAccountCreatedAsync(
            123,
            "Hannah Tan",
            "North View Secondary School",
            new DateTime(2026, 7, 1, 4, 0, 0, DateTimeKind.Utc),
            CancellationToken.None);

        EmailJob job = scheduler.Jobs.Should().ContainSingle().Subject;
        job.NotificationType.Should().Be(StudentAccountNotificationEmailService.AccountCreatedNotificationType);
        job.PersonId.Should().Be(123);
        job.Subject.Should().Be("Your Student Portal Account Is Ready");
        job.PlainTextBody.Should().Contain("Portal access ready");
        job.PlainTextBody.Should().Contain("North View Secondary School");
        job.PlainTextBody.Should().NotContain("Education Account has been created");
    }

    [Theory]
    [InlineData(true, StudentAccountNotificationEmailService.AccountDisabledNotificationType)]
    [InlineData(false, StudentAccountNotificationEmailService.AccountEnabledNotificationType)]
    public async Task SendStudentAccountStatusAsync_EnqueuesStatusMail(bool disabled, string expectedNotificationType)
    {
        RecordingEmailNotificationScheduler scheduler = new();
        StudentAccountNotificationEmailService service = new(scheduler, new TestEmailBrandingProvider());

        if (disabled)
        {
            await service.SendStudentAccountDisabledAsync(
                456,
                "Hannah Tan",
                new DateTime(2026, 7, 1, 4, 0, 0, DateTimeKind.Utc),
                CancellationToken.None);
        }
        else
        {
            await service.SendStudentAccountEnabledAsync(
                456,
                "Hannah Tan",
                new DateTime(2026, 7, 1, 4, 0, 0, DateTimeKind.Utc),
                CancellationToken.None);
        }

        EmailJob job = scheduler.Jobs.Should().ContainSingle().Subject;
        job.NotificationType.Should().Be(expectedNotificationType);
        job.PersonId.Should().Be(456);
        job.EntityType.Should().Be("StudentAccount");
        job.PlainTextBody.Should().Contain(disabled ? "disabled" : "enabled");
    }

    [Fact]
    public async Task SendStudentAccountCreatedAsync_WhenMailDisabled_DoesNotRecordJob()
    {
        RecordingEmailNotificationScheduler scheduler = new(enabled: false);
        StudentAccountNotificationEmailService service = new(scheduler, new TestEmailBrandingProvider());

        await service.SendStudentAccountCreatedAsync(
            123,
            "Hannah Tan",
            "North View Secondary School",
            DateTime.UtcNow,
            CancellationToken.None);

        scheduler.Jobs.Should().BeEmpty();
    }

    internal sealed record EmailJob(
        string NotificationType,
        long PersonId,
        string Subject,
        string PlainTextBody,
        string? HtmlBody,
        string? EntityType,
        string? EntityId);

    internal sealed class RecordingEmailNotificationScheduler(bool enabled = true) : IEmailNotificationScheduler
    {
        public List<EmailJob> Jobs { get; } = [];
        public bool IsEnabled => enabled;

        public Task<bool> EnqueueForPersonAsync(
            string notificationType,
            long personId,
            string subject,
            string plainTextBody,
            string? htmlBody,
            string? entityType,
            string? entityId,
            CancellationToken cancellationToken)
        {
            if (!IsEnabled)
            {
                return Task.FromResult(false);
            }

            Jobs.Add(new EmailJob(notificationType, personId, subject, plainTextBody, htmlBody, entityType, entityId));
            return Task.FromResult(true);
        }
    }

    internal sealed class TestEmailBrandingProvider : IEmailBrandingProvider
    {
        public string AppName => "Ministry of Education - Singapore";
        public string PaymentDashboardUrl => "http://localhost:5173/payments";
        public string FasPortalUrl => "http://localhost:5173/fas";
        public string AccountPortalUrl => "http://localhost:5173/account";
        public string CourseDetailUrl(long courseId) => $"http://localhost:5173/courses/{courseId}";
    }
}
