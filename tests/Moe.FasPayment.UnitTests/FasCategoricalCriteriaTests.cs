using FluentAssertions;
using Moe.Modules.FasPayment.Domain.Fas;
using Xunit;

namespace Moe.FasPayment.UnitTests;

public sealed class FasCategoricalCriteriaTests
{
    [Theory]
    [InlineData("NATIONALITY", "Singapore Citizen")]
    [InlineData("PARENT_NATIONALITY", "Vietnamese")]
    [InlineData("ACCOUNT_TYPE", "EDUCATION_ACCOUNT")]
    [InlineData("ACCOUNT_TYPE", "PERSONAL_ACCOUNT")]
    public void Supported_categorical_values_are_accepted(string criteriaType, string value)
    {
        FasTierCriteriaNationality result = FasTierCriteriaNationality.Create(1, criteriaType, value);

        result.Nationality.Should().Be(value);
    }

    [Theory]
    [InlineData("ACCOUNT_TYPE", "SAVINGS_ACCOUNT")]
    [InlineData("PARENT_NATIONALITY", "Atlantis")]
    [InlineData("AGE", "18")]
    public void Unsupported_categorical_values_are_rejected(string criteriaType, string value)
    {
        Action action = () => FasTierCriteriaNationality.Create(1, criteriaType, value);

        action.Should().Throw<ArgumentException>();
    }
}
