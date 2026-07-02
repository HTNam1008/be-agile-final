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
        var command = Command(
            new UpsertCampaignRuleDto("AGE", "GREATERTHAN", 10, null, null),
            new UpsertCampaignRuleDto("AGE", "LESSTHAN", 20, null, null));

        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.ErrorMessage.Contains("Duplicate criteria"));
    }

    [Fact]
    public void Validator_ShouldAccept_DuplicateCriteriaAcrossGroups()
    {
        var command = new UpsertCampaignRulesCommand(1,
        [
            new UpsertRuleGroupDto([new UpsertCampaignRuleDto("AGE", "GREATERTHAN", 10, null, null)]),
            new UpsertRuleGroupDto([new UpsertCampaignRuleDto("AGE", "LESSTHAN", 20, null, null)])
        ]);

        var result = _validator.Validate(command);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validator_ShouldReject_TextValueForAge()
    {
        var command = Command(new UpsertCampaignRuleDto("AGE", "EQUALS", 15, null, "15"));

        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validator_ShouldAccept_EmptyGroupsAsRuleClear()
    {
        var command = new UpsertCampaignRulesCommand(1, []);

        var result = _validator.Validate(command);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validator_ShouldReject_InvalidJsonArrayForInOperator()
    {
        var command = Command(new UpsertCampaignRuleDto("LEVEL", "IN", null, null, "Primary,Secondary")); // Not JSON

        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.ErrorMessage.Contains("JSON array"));
    }

    [Fact]
    public void Validator_ShouldAccept_ValidJsonArrayForInOperator()
    {
        var command = Command(new UpsertCampaignRuleDto("LEVEL", "IN", null, null, "[\"Primary\", \"Secondary\"]"));

        var result = _validator.Validate(command);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validator_ShouldAccept_ValidBetweenNumeric()
    {
        var command = Command(new UpsertCampaignRuleDto("ACCOUNTBALANCE", "BETWEEN", 100m, 500m, null));

        var result = _validator.Validate(command);
        result.IsValid.Should().BeTrue();
    }

    private static UpsertCampaignRulesCommand Command(params UpsertCampaignRuleDto[] criteria)
        => new(1, [new UpsertRuleGroupDto(criteria.ToList())]);
}
