using FluentAssertions;
using Moe.Modules.EducationAccountTopUp.Application.TopUps;
using Xunit;

namespace Moe.EducationAccountTopUp.UnitTests;

public sealed class TopUpDisplayMaskerTests
{
    [Theory]
    [InlineData("EA-DEMO-0002", "EA-****-0002")]
    [InlineData("12345678", "****5678")]
    [InlineData("1234", "****")]
    public void Should_Mask_Account_Number(string value, string expected)
    {
        TopUpDisplayMasker.MaskAccountNumber(value).Should().Be(expected);
    }

    [Theory]
    [InlineData("DEMO-STU-0001", "*********0001")]
    [InlineData("S001", "****")]
    public void Should_Mask_Student_Number(string value, string expected)
    {
        TopUpDisplayMasker.MaskStudentNumber(value).Should().Be(expected);
    }
}
