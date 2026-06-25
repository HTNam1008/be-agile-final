using FluentAssertions;
using Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;
using Xunit;

namespace Moe.EducationAccountTopUp.UnitTests.Domain;

public sealed class EducationAccountAutomaticOpeningTests
{
    [Fact]
    public void OpenAutomatically_UsesAutomaticModeAndEligibilityReason()
    {
        DateTimeOffset now = new(2026, 6, 24, 2, 0, 0, TimeSpan.Zero);

        var result = EducationAccount.OpenAutomatically(123, "PSEA-00000123", now);

        result.IsSuccess.Should().BeTrue();
        result.Value.PersonId.Should().Be(123);
        result.Value.AccountNumber.Should().Be("PSEA-00000123");
        result.Value.StatusCode.Should().Be(AccountStatuses.Active);
        result.Value.OpeningModeCode.Should().Be(AccountOpeningModeCodes.Automatic);
        result.Value.OpeningReasonCode.Should().Be(EducationAccountOpeningReasonCodes.AutoEligibility);
        result.Value.OpeningRemarks.Should().BeNull();
        result.Value.OpenedByUserId.Should().BeNull();
    }

    [Fact]
    public void CloseAutomatically_WhenActive_ClosesWithAutoAgeLimitReasonAndDoesNotChangeBalance()
    {
        DateTimeOffset openedAt = new(2026, 6, 24, 2, 0, 0, TimeSpan.Zero);
        DateTimeOffset closedAt = openedAt.AddDays(1);
        EducationAccount account = EducationAccount.OpenAutomatically(123, "PSEA-00000123", openedAt).Value;
        account.UpdateBalance(125.50m);

        var result = account.CloseAutomatically(closedAt);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
        account.StatusCode.Should().Be(AccountStatuses.Closed);
        account.ClosedAtUtc.Should().Be(closedAt);
        account.ClosingReasonCode.Should().Be(EducationAccountClosingReasonCodes.AutoAgeLimit);
        account.ClosingRemarks.Should().BeNull();
        account.ClosedByLoginAccountId.Should().BeNull();
        account.CachedBalance.Should().Be(125.50m);
    }

    [Fact]
    public void CloseAutomatically_WhenAlreadyClosed_IsNoOpSuccess()
    {
        DateTimeOffset openedAt = new(2026, 6, 24, 2, 0, 0, TimeSpan.Zero);
        DateTimeOffset firstClosedAt = openedAt.AddDays(1);
        DateTimeOffset secondClosedAt = openedAt.AddDays(2);
        EducationAccount account = EducationAccount.OpenAutomatically(123, "PSEA-00000123", openedAt).Value;
        account.CloseAutomatically(firstClosedAt).IsSuccess.Should().BeTrue();

        var result = account.CloseAutomatically(secondClosedAt);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeFalse();
        account.ClosedAtUtc.Should().Be(firstClosedAt);
        account.ClosingReasonCode.Should().Be(EducationAccountClosingReasonCodes.AutoAgeLimit);
    }
}
