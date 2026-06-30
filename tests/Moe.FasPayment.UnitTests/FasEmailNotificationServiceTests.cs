using System.Reflection;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moe.Modules.FasPayment;
using Moe.Modules.CourseBilling;
using Moe.Modules.FasPayment.Application.Notifications;
using Moe.Modules.FasPayment.Domain.Fas;
using Moe.Modules.IdentityPlatform.IGateway.People;
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

        RecordingRecipientResolver recipients = new();
        RecordingEmailGateway mailGateway = new();
        FasEmailNotificationService service = new(
            db,
            recipients,
            mailGateway,
            new FixedEmailDeliverySwitch(),
            NullLogger<FasEmailNotificationService>.Instance);

        await service.SendSubmissionAcknowledgementAsync(application.Id, CancellationToken.None);

        recipients.ProvidedEmail.Should().Be("fas-applicant@example.com");
        mailGateway.Messages.Should().ContainSingle();
        mailGateway.Messages.Single().ToEmail.Should().Be("fas-applicant@example.com");
    }

    [Fact]
    public async Task SubmissionAcknowledgement_WhenMailDeliveryDisabled_DoesNotCallRecipientResolverOrGateway()
    {
        await using MoeDbContext db = CreateDbContext();
        RecordingRecipientResolver recipients = new();
        RecordingEmailGateway mailGateway = new();
        FasEmailNotificationService service = new(
            db,
            recipients,
            mailGateway,
            new FixedEmailDeliverySwitch(isEnabled: false),
            NullLogger<FasEmailNotificationService>.Instance);

        await service.SendSubmissionAcknowledgementAsync(999, CancellationToken.None);

        recipients.ProvidedEmail.Should().BeNull();
        mailGateway.Messages.Should().BeEmpty();
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

    private sealed class RecordingRecipientResolver : IEmailRecipientResolver
    {
        public string? ProvidedEmail { get; private set; }

        public Task<EmailRecipient?> ResolveForPersonAsync(long personId, CancellationToken cancellationToken)
            => throw new InvalidOperationException("FAS must use the application email.");

        public EmailRecipient? ResolveProvided(string? emailAddress)
        {
            ProvidedEmail = emailAddress;
            return string.IsNullOrWhiteSpace(emailAddress)
                ? null
                : new EmailRecipient(emailAddress, EmailRecipientSourceCodes.Provided);
        }
    }

    private sealed class RecordingEmailGateway : IEmailDeliveryGateway
    {
        public List<EmailDeliveryMessage> Messages { get; } = [];

        public Task<Result> SendAsync(EmailDeliveryMessage message, CancellationToken cancellationToken)
        {
            Messages.Add(message);
            return Task.FromResult(Result.Success());
        }
    }

    private sealed class FixedEmailDeliverySwitch(bool isEnabled = true) : IEmailDeliverySwitch
    {
        public bool IsEnabled { get; } = isEnabled;
    }
}
