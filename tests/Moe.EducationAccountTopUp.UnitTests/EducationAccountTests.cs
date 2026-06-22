using FluentAssertions;
using Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;
using Xunit;

namespace Moe.EducationAccountTopUp.UnitTests;

public sealed class EducationAccountTests
{
    [Fact]
    public void Manual_open_requires_reason_and_remarks()
    {
        var result = EducationAccount.OpenManual(1, "EA-001", DateTimeOffset.UtcNow, "", "", 99);
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AccountErrors.ManualReasonRequired);
    }

    [Fact]
    public void Closed_account_cannot_be_closed_twice()
    {
        var account = EducationAccount.OpenManual(1, "EA-001", DateTimeOffset.UtcNow, "EXCEPTION", "New citizen", 99).Value;
        account.CloseManual(DateTimeOffset.UtcNow, "DUPLICATE_ACCOUNT", "Upstream notification", 1001).IsSuccess.Should().BeTrue();
        account.CloseManual(DateTimeOffset.UtcNow, "DUPLICATE_ACCOUNT", "Retry", 1001).Error.Should().Be(AccountErrors.AlreadyClosed);
    }

    [Fact]
    public void CloseManual_OnActiveAccount_TransitionsToClosedAndSetsActor()
    {
        var now = new DateTimeOffset(2026, 6, 22, 8, 0, 0, TimeSpan.Zero);
        var account = EducationAccount.OpenManual(1, "EA-002", now, "EXCEPTION", "New citizen", 99).Value;

        var result = account.CloseManual(now, "STUDENT_INELIGIBLE", "Student no longer eligible", 1001);

        result.IsSuccess.Should().BeTrue();
        account.StatusCode.Should().Be(AccountStatuses.Closed);
        account.ClosedAtUtc.Should().Be(now);
        account.ClosingReasonCode.Should().Be("STUDENT_INELIGIBLE");
        account.ClosingRemarks.Should().Be("Student no longer eligible");
        account.ClosedByLoginAccountId.Should().Be(1001);
    }

    [Fact]
    public void CloseManual_OnAlreadyClosedAccount_ReturnsFailure()
    {
        var now = new DateTimeOffset(2026, 6, 22, 8, 0, 0, TimeSpan.Zero);
        var account = EducationAccount.OpenManual(1, "EA-003", now, "EXCEPTION", "New citizen", 99).Value;
        account.CloseManual(now, "STUDENT_INELIGIBLE", "Initial closure", 1001).IsSuccess.Should().BeTrue();
        var closedAt = account.ClosedAtUtc;

        var result = account.CloseManual(now.AddHours(1), "OTHER", "Retry", 2002);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AccountErrors.AlreadyClosed);
        account.ClosedAtUtc.Should().Be(closedAt);
        account.ClosingReasonCode.Should().Be("STUDENT_INELIGIBLE");
        account.ClosedByLoginAccountId.Should().Be(1001);
    }

    [Fact]
    public void CloseManual_AllowsOptionalRemarks()
    {
        var now = new DateTimeOffset(2026, 6, 22, 8, 0, 0, TimeSpan.Zero);
        var account = EducationAccount.OpenManual(1, "EA-004", now, "EXCEPTION", "New citizen", 99).Value;

        var result = account.CloseManual(now, "OTHER", null, 1001);

        result.IsSuccess.Should().BeTrue();
        account.ClosingRemarks.Should().BeNull();
    }
}
