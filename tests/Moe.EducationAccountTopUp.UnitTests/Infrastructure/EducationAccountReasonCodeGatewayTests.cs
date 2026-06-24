using FluentAssertions;
using Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;
using Moe.Modules.EducationAccountTopUp.Infrastructure.Gateways;
using Xunit;

namespace Moe.EducationAccountTopUp.UnitTests.Infrastructure;

public sealed class EducationAccountReasonCodeGatewayTests
{
    [Fact]
    public void GetCloseReasonOptions_ReturnsDomainCloseReasonConstants()
    {
        EducationAccountReasonCodeGateway gateway = new();

        var options = gateway.GetCloseReasonOptions();

        options.Select(x => x.Value).Should().Equal(
            EducationAccountClosingReasonCodes.StudentIneligible,
            EducationAccountClosingReasonCodes.DuplicateAccount,
            EducationAccountClosingReasonCodes.AdminError,
            EducationAccountClosingReasonCodes.Other);
    }

    [Fact]
    public void GetOpenReasonOptions_ReturnsEmptyUntilOpenReasonEnumExists()
    {
        EducationAccountReasonCodeGateway gateway = new();

        gateway.GetOpenReasonOptions().Should().BeEmpty();
    }
}
