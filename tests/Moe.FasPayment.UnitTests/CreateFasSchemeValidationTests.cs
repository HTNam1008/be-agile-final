using FluentAssertions;
using Moe.Modules.FasPayment.Application.AdminFasSchemes;
using Moe.Modules.FasPayment.Contracts.AdminFasSchemes;
using Xunit;

namespace Moe.FasPayment.UnitTests;

public sealed class CreateFasSchemeValidationTests
{
    private readonly CreateFasSchemeRequestValidator _validator = new();

    [Fact]
    public void Valid_wizard_payload_passes() => _validator.Validate(FasSchemeTestData.ValidRequest()).IsValid.Should().BeTrue();

    [Theory]
    [InlineData("PERCENTAGE", 0, false)]
    [InlineData("PERCENTAGE", 1, true)]
    [InlineData("PERCENTAGE", 100, true)]
    [InlineData("PERCENTAGE", 100.01, false)]
    [InlineData("FIXED", 0, true)]
    [InlineData("FIXED", -0.01, false)]
    public void Subsidy_boundaries_match_v4(string type, double value, bool valid)
    {
        CreateFasSchemeRequest request = FasSchemeTestData.ValidRequest() with
        {
            SubsidyType = type,
            Tiers = [FasSchemeTestData.ValidRequest().Tiers[0] with { SubsidyValue = (decimal)value }]
        };
        _validator.Validate(request).IsValid.Should().Be(valid);
    }

    [Fact]
    public void Reversed_dates_and_default_dates_fail()
    {
        CreateFasSchemeRequest source = FasSchemeTestData.ValidRequest();
        _validator.Validate(source with { StartDate = source.EndDate, EndDate = source.StartDate }).IsValid.Should().BeFalse();
        _validator.Validate(source with { StartDate = default, EndDate = default }).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Start_date_before_today_fails()
    {
        CreateFasSchemeRequest source = FasSchemeTestData.ValidRequest();
        DateOnly yesterday = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
        _validator.Validate(source with { StartDate = yesterday, EndDate = yesterday.AddDays(30) }).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Duplicate_or_nonpositive_courses_fail_while_global_scheme_is_allowed()
    {
        _validator.Validate(FasSchemeTestData.ValidRequest() with { CourseIds = [] }).IsValid.Should().BeTrue();
        _validator.Validate(FasSchemeTestData.ValidRequest() with { CourseIds = [1, 1] }).IsValid.Should().BeFalse();
        _validator.Validate(FasSchemeTestData.ValidRequest() with { CourseIds = [0] }).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Tier_and_template_orders_must_be_contiguous()
    {
        CreateFasSchemeRequest source = FasSchemeTestData.ValidRequest();
        _validator.Validate(source with { CriteriaTemplate = [new("AGE", null, 2)] }).IsValid.Should().BeFalse();
        _validator.Validate(source with { Tiers = [source.Tiers[0] with { DisplayOrder = 2 }] }).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Connector_shape_is_enforced()
    {
        CreateFasSchemeRequest source = FasSchemeTestData.ValidRequest();
        _validator.Validate(source with { CriteriaTemplate = [new("AGE", null, 1), new("NATIONALITY", null, 2)] }).IsValid.Should().BeFalse();
        _validator.Validate(source with { CriteriaTemplate = [new("AGE", "AND", 1)] }).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Numeric_criteria_require_complete_ordered_bounds()
    {
        CreateFasSchemeRequest source = FasSchemeTestData.ValidRequest();
        CreateFasTierRequest tier = source.Tiers[0];
        _validator.Validate(source with { Tiers = [tier with { CriteriaValues = [new(1, null, 18, null), tier.CriteriaValues[1]] }] }).IsValid.Should().BeFalse();
        _validator.Validate(source with { Tiers = [tier with { CriteriaValues = [new(1, 19, 18, null), tier.CriteriaValues[1]] }] }).IsValid.Should().BeFalse();
        _validator.Validate(source with { Tiers = [tier with { CriteriaValues = [new(1, -1, 18, null), tier.CriteriaValues[1]] }] }).IsValid.Should().BeFalse();
        _validator.Validate(source with { Tiers = [tier with { CriteriaValues = [new(1, 13, 121, null), tier.CriteriaValues[1]] }] }).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Nationality_criteria_reject_bounds_empty_and_unknown_values()
    {
        CreateFasSchemeRequest source = FasSchemeTestData.ValidRequest();
        CreateFasTierRequest tier = source.Tiers[0];
        _validator.Validate(source with { Tiers = [tier with { CriteriaValues = [tier.CriteriaValues[0], new(2, 1, null, ["Singapore Citizen"])] }] }).IsValid.Should().BeFalse();
        _validator.Validate(source with { Tiers = [tier with { CriteriaValues = [tier.CriteriaValues[0], new(2, null, null, [])] }] }).IsValid.Should().BeFalse();
        _validator.Validate(source with { Tiers = [tier with { CriteriaValues = [tier.CriteriaValues[0], new(2, null, null, ["Unknown"])] }] }).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Missing_or_duplicate_tier_values_fail()
    {
        CreateFasSchemeRequest source = FasSchemeTestData.ValidRequest();
        CreateFasTierRequest tier = source.Tiers[0];
        _validator.Validate(source with { Tiers = [tier with { CriteriaValues = [] }] }).IsValid.Should().BeFalse();
        _validator.Validate(source with { Tiers = [tier with { CriteriaValues = [tier.CriteriaValues[0], tier.CriteriaValues[0]] }] }).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Empty_or_null_nested_collections_return_validation_failures_not_exceptions()
    {
        CreateFasSchemeRequest source = FasSchemeTestData.ValidRequest();
        _validator.Invoking(x => x.Validate(source with { Tiers = null! })).Should().NotThrow();
        _validator.Validate(source with { Tiers = null! }).IsValid.Should().BeFalse();
        _validator.Invoking(x => x.Validate(source with { Tiers = [source.Tiers[0] with { CriteriaValues = null! }] })).Should().NotThrow();
    }

    [Fact]
    public void Per_tier_scheme_fields_are_forbidden()
    {
        CreateFasSchemeRequest source = FasSchemeTestData.ValidRequest();
        _validator.Validate(source with { Tiers = [source.Tiers[0] with { GrantCode = "OLD", SubsidyType = "FIXED" }] }).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Duplicate_template_types_and_tier_labels_fail()
    {
        CreateFasSchemeRequest source = FasSchemeTestData.ValidRequest();
        _validator.Validate(source with { CriteriaTemplate = [new("AGE", "AND", 1), new("AGE", null, 2)] }).IsValid.Should().BeFalse();
        _validator.Validate(source with { Tiers = [source.Tiers[0], source.Tiers[0] with { DisplayOrder = 2 }] }).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("DRAFT", true)]
    [InlineData("ACTIVE", true)]
    [InlineData("RETIRED", true)]
    [InlineData("DISABLED", true)]
    [InlineData("DELETED", false)]
    [InlineData("PUBLISHED", false)]
    public void List_status_validation_is_explicit(string? status, bool valid)
        => new ListFasSchemesRequestValidator().Validate(new ListFasSchemesRequest(status, null)).IsValid.Should().Be(valid);
}
