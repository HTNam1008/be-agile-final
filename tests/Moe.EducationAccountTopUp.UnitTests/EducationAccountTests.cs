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
        account.CloseManual(DateTimeOffset.UtcNow, "DECEASED", "Upstream notification").IsSuccess.Should().BeTrue();
        account.CloseManual(DateTimeOffset.UtcNow, "DECEASED", "Retry").Error.Should().Be(AccountErrors.AlreadyClosed);
    }
}
