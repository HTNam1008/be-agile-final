using FluentAssertions;
using Moe.Modules.EducationAccountTopUp.Application.TopUps.UpsertCampaignRules;
using Moe.Modules.EducationAccountTopUp.Contracts.TopUps.Enums;
using Xunit;

namespace Moe.EducationAccountTopUp.UnitTests;

public sealed class TopUpRuleValidationTests
{
    private readonly UpsertCampaignRulesCommandValidator _validator = new();

    [Fact]
    public void Validator_ShouldReject_DuplicateCriteria()
    {
        var command = new UpsertCampaignRulesCommand(1, new List<UpsertCampaignRuleDto>
        {
            new("AGE", "GREATERTHAN", 10, null, null),
            new("AGE", "LESSTHAN", 20, null, null)
        });

        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.ErrorMessage.Contains("Duplicate criteria"));
    }

    [Fact]
    public void Validator_ShouldReject_TextValueForAge()
    {
        var command = new UpsertCampaignRulesCommand(1, new List<UpsertCampaignRuleDto>
        {
            new("AGE", "EQUALS", null, null, "15")
        });

        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validator_ShouldReject_InvalidJsonArrayForInOperator()
    {
        var command = new UpsertCampaignRulesCommand(1, new List<UpsertCampaignRuleDto>
        {
            new("LEVEL", "IN", null, null, "Primary,Secondary") // Not JSON
        });

        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.ErrorMessage.Contains("JSON array"));
    }

    [Fact]
    public void Validator_ShouldAccept_ValidJsonArrayForInOperator()
    {
        var command = new UpsertCampaignRulesCommand(1, new List<UpsertCampaignRuleDto>
        {
            new("LEVEL", "IN", null, null, "[\"Primary\", \"Secondary\"]")
        });

        var result = _validator.Validate(command);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validator_ShouldAccept_ValidBetweenNumeric()
    {
        var command = new UpsertCampaignRulesCommand(1, new List<UpsertCampaignRuleDto>
        {
            new("ACCOUNTBALANCE", "BETWEEN", 100m, 500m, null)
        });

        var result = _validator.Validate(command);
        result.IsValid.Should().BeTrue();
    }
}
