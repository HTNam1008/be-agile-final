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
}
