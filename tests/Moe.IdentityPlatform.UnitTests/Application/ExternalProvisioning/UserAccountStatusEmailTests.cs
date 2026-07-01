using FluentAssertions;
using Moe.Application.Abstractions.Audit;
using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Persistence;
using Moe.Modules.IdentityPlatform.Application.ExternalProvisioning.DisableUserAccount;
using Moe.Modules.IdentityPlatform.Application.ExternalProvisioning.EnableUserAccount;
using Moe.Modules.IdentityPlatform.Application.Students;
using Moe.Modules.IdentityPlatform.Domain.Iam;
using Moe.Modules.IdentityPlatform.IGateway.Repositories;
using Moe.IdentityPlatform.UnitTests.Application.Students;
using Xunit;

namespace Moe.IdentityPlatform.UnitTests.Application.ExternalProvisioning;

public sealed class UserAccountStatusEmailTests
{
    private readonly DateTimeOffset _now = new(2026, 7, 1, 4, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task DisableUserAccountHandler_OnStudentAccount_EnqueuesDisabledMail()
    {
        UserAccount account = UserAccount.CreateStudentSingpass(123, "issuer", "subject", "Hannah Tan", 99, _now.UtcDateTime);
        FakeUserAccountRepository accounts = new(account);
        RecordingProfileRepository profiles = new(CreateProfile(123));
        StudentAccountNotificationEmailService notifications = CreateNotificationService(out var scheduler);
        DisableUserAccountHandler handler = new(
            accounts,
            profiles,
            new TestClock(_now),
            new FakeAuditService(),
            new FakeUnitOfWork(),
            notifications);

        var result = await handler.Handle(new DisableUserAccountCommand(1), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        scheduler.Jobs.Should().ContainSingle().Subject.NotificationType
            .Should().Be(StudentAccountNotificationEmailService.AccountDisabledNotificationType);
        scheduler.Jobs[0].PersonId.Should().Be(123);
    }

    [Fact]
    public async Task EnableUserAccountHandler_OnStudentAccount_EnqueuesEnabledMail()
    {
        UserAccount account = UserAccount.CreateStudentSingpass(123, "issuer", "subject", "Hannah Tan", 99, _now.UtcDateTime);
        account.Disable(_now.UtcDateTime);
        FakeUserAccountRepository accounts = new(account);
        RecordingProfileRepository profiles = new(CreateProfile(123));
        StudentAccountNotificationEmailService notifications = CreateNotificationService(out var scheduler);
        EnableUserAccountHandler handler = new(
            accounts,
            profiles,
            new TestClock(_now),
            new FakeAuditService(),
            new FakeUnitOfWork(),
            notifications);

        var result = await handler.Handle(new EnableUserAccountCommand(1), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        scheduler.Jobs.Should().ContainSingle().Subject.NotificationType
            .Should().Be(StudentAccountNotificationEmailService.AccountEnabledNotificationType);
        scheduler.Jobs[0].PersonId.Should().Be(123);
    }

    [Fact]
    public async Task DisableUserAccountHandler_OnAdminAccount_DoesNotEnqueueStudentMail()
    {
        UserAccount account = UserAccount.CreateAdmin(
            "issuer",
            "subject",
            null,
            null,
            "admin@example.com",
            "Admin User",
            RoleCodes.SchoolAdmin,
            1,
            99,
            _now.UtcDateTime);
        FakeUserAccountRepository accounts = new(account);
        StudentAccountNotificationEmailService notifications = CreateNotificationService(out var scheduler);
        DisableUserAccountHandler handler = new(
            accounts,
            new RecordingProfileRepository(null),
            new TestClock(_now),
            new FakeAuditService(),
            new FakeUnitOfWork(),
            notifications);

        var result = await handler.Handle(new DisableUserAccountCommand(1), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        scheduler.Jobs.Should().BeEmpty();
    }

    private static StudentAccountNotificationEmailService CreateNotificationService(
        out StudentAccountNotificationEmailServiceTests.RecordingEmailNotificationScheduler scheduler)
    {
        scheduler = new StudentAccountNotificationEmailServiceTests.RecordingEmailNotificationScheduler();
        return new StudentAccountNotificationEmailService(
            scheduler,
            new StudentAccountNotificationEmailServiceTests.TestEmailBrandingProvider());
    }

    private static StudentProfileSummary CreateProfile(long personId)
        => new(
            personId,
            $"EXT-{personId}",
            "S****123A",
            "Hannah Tan",
            new DateOnly(2000, 1, 1),
            "SG",
            "CITIZEN",
            "hannah@example.com",
            "hannah@example.com",
            null,
            null,
            null,
            null,
            new DateTime(2026, 7, 1, 4, 0, 0, DateTimeKind.Utc),
            10,
            1,
            "SCH",
            "North View Secondary School",
            "S001",
            "2026",
            "SEC_1",
            "1A",
            "ACTIVE",
            new DateOnly(2026, 1, 1),
            null);

    private sealed class FakeUserAccountRepository(UserAccount? account) : IUserAccountRepository
    {
        public Task<UserAccount?> FindByIdAsync(long userAccountId, CancellationToken cancellationToken)
            => Task.FromResult(account);

        public Task<bool> ExistsAdminByEmailAsync(string normalizedEmail, CancellationToken cancellationToken)
            => Task.FromResult(false);

        public Task<bool> ExistsSingpassForPersonAsync(long personId, CancellationToken cancellationToken)
            => Task.FromResult(false);

        public Task<UserAccount?> DisableAsync(long userAccountId, DateTime utcNow, CancellationToken cancellationToken)
        {
            account?.Disable(utcNow);
            return Task.FromResult(account);
        }

        public Task<UserAccount?> EnableAsync(long userAccountId, DateTime utcNow, CancellationToken cancellationToken)
        {
            account?.Enable(utcNow);
            return Task.FromResult(account);
        }

        public Task<UserAccount?> UpdateContactDetailsAsync(
            long userAccountId,
            string? contactEmail,
            string? contactMobile,
            DateTime utcNow,
            CancellationToken cancellationToken)
            => Task.FromResult(account);
    }

    private sealed class RecordingProfileRepository(StudentProfileSummary? profile) : IStudentProfileRepository
    {
        public Task<StudentProfileSummary?> GetProfileSummaryAsync(long personId, DateOnly today, CancellationToken cancellationToken)
            => Task.FromResult(profile);

        public Task<UpdatePreferredContactResult> UpdatePreferredContactAsync(
            long personId,
            string? preferredEmail,
            string? preferredMobile,
            string? preferredAddress,
            DateTime? expectedUpdatedAtUtc,
            DateTime utcNow,
            CancellationToken cancellationToken)
            => Task.FromResult(new UpdatePreferredContactResult(UpdatePreferredContactStatus.NotFound, null));
    }

    private sealed class TestClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
    }

    private sealed class FakeAuditService : IAuditService
    {
        public Task RecordAsync(
            string actionCode,
            string entityTypeCode,
            string entityId,
            string? detailsJson = null,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task RecordSchoolActionAsync(SchoolAuditContext context, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
