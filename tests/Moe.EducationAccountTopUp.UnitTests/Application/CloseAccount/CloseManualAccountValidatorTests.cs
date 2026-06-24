using FluentAssertions;
using Moe.Modules.EducationAccountTopUp.Application.CloseAccount;
using Xunit;

namespace Moe.EducationAccountTopUp.UnitTests.Application.CloseAccount;

public sealed class CloseManualAccountValidatorTests
{
    [Fact]
    public void CloseManualAccountValidator_RejectsUnknownReasonCode()
    {
        CloseManualAccountValidator validator = new();
        CloseManualAccountCommand command = new(1, "NOT_A_REASON", "Manual closure");

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.PropertyName == nameof(CloseManualAccountCommand.ReasonCode));
    }
}
