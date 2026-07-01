using System.Reflection;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moe.Modules.FasPayment;
using Moe.Modules.CourseBilling;
using Moe.Modules.FasPayment.Application.Notifications;
using Moe.Modules.FasPayment.Domain.Fas;
using Moe.Modules.MailDelivery.IGateway;
using Moe.SharedKernel.Results;
using Moe.StudentFinance.Persistence;
using Xunit;

namespace Moe.FasPayment.UnitTests;

public sealed class FasEmailNotificationServiceTests
{
    [Fact]
    public async Task SubmissionAcknowledgement_UsesEmailStoredOnApplication()
    {
        await using MoeDbContext db = CreateDbContext();
        DateTime now = new(2026, 6, 29, 0, 0, 0, DateTimeKind.Utc);
        FasScheme scheme = FasScheme.CreateDraft(
            "MOE-FAS",
            "GRANT",
            "MOE Financial Assistance",
            null,
            new DateOnly(2026, 6, 29),
            new DateOnly(2027, 6, 29),
            1,
            now);
        SetId(scheme, 101);
        FasApplication application = FasApplication.CreateDraft(
            "FAS-TEST",
            501,
            scheme.Id,
            "STU-501",
            "Test Student",
            "S****501A",
            new DateOnly(2005, 1, 1),
            "SG",
            "90000000",
            "Singapore",
            "fas-applicant@example.com",
            2,
            "Test School",
            "EDUCATION_ACCOUNT",
            1,
            now);
        SetId(application, 201);
        db.AddRange(scheme, application);
        await db.SaveChangesAsync();

        RecordingEmailNotificationQueue mailQueue = new();
        FasEmailNotificationService service = new(
            db,
            mailQueue,
            new FixedEmailDeliverySwitch(),
            NullLogger<FasEmailNotificationService>.Instance);

        await service.SendSubmissionAcknowledgementAsync(application.Id, CancellationToken.None);

        mailQueue.Jobs.Should().ContainSingle();
        EmailNotificationJob job = mailQueue.Jobs.Single();
        job.NotificationType.Should().Be("NOTI-05");
        job.ProvidedEmail.Should().Be("fas-applicant@example.com");
        job.Subject.Should().Be("We've Received Your FAS Application");
    }

    [Fact]
    public async Task SubmissionAcknowledgement_WhenMailDeliveryDisabled_DoesNotEnqueueEmail()
    {
        await using MoeDbContext db = CreateDbContext();
        RecordingEmailNotificationQueue mailQueue = new();
        FasEmailNotificationService service = new(
            db,
            mailQueue,
            new FixedEmailDeliverySwitch(isEnabled: false),
            NullLogger<FasEmailNotificationService>.Instance);

        await service.SendSubmissionAcknowledgementAsync(999, CancellationToken.None);

        mailQueue.Jobs.Should().BeEmpty();
    }

    private static MoeDbContext CreateDbContext()
    {
        DbContextOptions<MoeDbContext> options = new DbContextOptionsBuilder<MoeDbContext>()
            .UseInMemoryDatabase($"fas-email-{Guid.NewGuid():N}")
            .Options;
        return new MoeDbContext(options, [
            new CourseBillingModelConfiguration(),
            new FasPaymentModelConfiguration()
        ]);
    }

    private static void SetId<T>(T entity, long id)
        => typeof(T).GetProperty("Id", BindingFlags.Instance | BindingFlags.Public)!.SetValue(entity, id);

    private sealed class RecordingEmailNotificationQueue : IEmailNotificationQueue
    {
        public List<EmailNotificationJob> Jobs { get; } = [];

        public ValueTask<Result> EnqueueAsync(
            EmailNotificationJob job,
            CancellationToken cancellationToken)
        {
            Jobs.Add(job);
            return ValueTask.FromResult(Result.Success());
        }

        public async IAsyncEnumerable<EmailNotificationJob> ReadAllAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            yield break;
        }
    }

    private sealed class FixedEmailDeliverySwitch(bool isEnabled = true) : IEmailDeliverySwitch
    {
        public bool IsEnabled { get; } = isEnabled;
    }
}
