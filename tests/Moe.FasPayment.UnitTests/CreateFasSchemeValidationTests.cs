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
    [InlineData("PERCENTAGE", 0, true)]
    [InlineData("PERCENTAGE", 100, true)]
    [InlineData("PERCENTAGE", 100.01, false)]
    [InlineData("FIXED", 0, true)]
    [InlineData("FIXED", -0.01, false)]
    public void Subsidy_boundaries_match_v4(string type, double value, bool valid)
    {
        CreateFasSchemeRequest request = FasSchemeTestData.ValidRequest() with
        {
            Tiers = [FasSchemeTestData.ValidRequest().Tiers[0] with { SubsidyType = type, SubsidyValue = (decimal)value }]
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
    public void Duplicate_or_nonpositive_courses_fail()
    {
        _validator.Validate(FasSchemeTestData.ValidRequest() with { CourseIds = [1, 1] }).IsValid.Should().BeFalse();
        _validator.Validate(FasSchemeTestData.ValidRequest() with { CourseIds = [0] }).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Tier_and_criteria_orders_must_be_contiguous()
    {
        CreateFasSchemeRequest source = FasSchemeTestData.ValidRequest();
        _validator.Validate(source with { Tiers = [source.Tiers[0] with { Criteria = [source.Tiers[0].Criteria[0] with { DisplayOrder = 2 }] }] }).IsValid.Should().BeFalse();
        _validator.Validate(source with { Tiers = [source.Tiers[0] with { DisplayOrder = 2 }] }).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Connector_shape_is_enforced()
    {
        CreateFasSchemeRequest source = FasSchemeTestData.ValidRequest();
        _validator.Validate(source with { Tiers = [source.Tiers[0] with { Criteria = [source.Tiers[0].Criteria[0] with { ConnectorToNext = null }, source.Tiers[0].Criteria[1]] }] }).IsValid.Should().BeFalse();
        _validator.Validate(source with { Tiers = [source.Tiers[0] with { Criteria = [source.Tiers[0].Criteria[0] with { ConnectorToNext = "AND" }] }] }).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Numeric_criteria_require_complete_ordered_bounds()
    {
        CreateFasSchemeRequest source = FasSchemeTestData.ValidRequest();
        CreateFasTierRequest tier = source.Tiers[0];
        _validator.Validate(source with { Tiers = [tier with { Criteria = [tier.Criteria[0] with { NumberFrom = null, NumberTo = 18 }, tier.Criteria[1]] }] }).IsValid.Should().BeFalse();
        _validator.Validate(source with { Tiers = [tier with { Criteria = [tier.Criteria[0] with { NumberFrom = 19, NumberTo = 18 }, tier.Criteria[1]] }] }).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Nationality_criteria_reject_bounds_empty_and_unknown_values()
    {
        CreateFasSchemeRequest source = FasSchemeTestData.ValidRequest();
        CreateFasTierRequest tier = source.Tiers[0];
        _validator.Validate(source with { Tiers = [tier with { Criteria = [tier.Criteria[0], tier.Criteria[1] with { NumberFrom = 1 }] }] }).IsValid.Should().BeFalse();
        _validator.Validate(source with { Tiers = [tier with { Criteria = [tier.Criteria[0], tier.Criteria[1] with { Nationalities = [] }] }] }).IsValid.Should().BeFalse();
        _validator.Validate(source with { Tiers = [tier with { Criteria = [tier.Criteria[0], tier.Criteria[1] with { Nationalities = ["Unknown"] }] }] }).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Missing_or_duplicate_tier_values_fail()
    {
        CreateFasSchemeRequest source = FasSchemeTestData.ValidRequest();
        CreateFasTierRequest tier = source.Tiers[0];
        _validator.Validate(source with { Tiers = [tier with { Criteria = [] }] }).IsValid.Should().BeFalse();
        _validator.Validate(source with { Tiers = [tier with { Criteria = [tier.Criteria[0], tier.Criteria[0]] }] }).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Empty_or_null_nested_collections_return_validation_failures_not_exceptions()
    {
        CreateFasSchemeRequest source = FasSchemeTestData.ValidRequest();
        _validator.Invoking(x => x.Validate(source with { Tiers = null! })).Should().NotThrow();
        _validator.Validate(source with { Tiers = null! }).IsValid.Should().BeFalse();
        _validator.Invoking(x => x.Validate(source with { Tiers = [source.Tiers[0] with { Criteria = null! }] })).Should().NotThrow();
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("ACTIVE", true)]
    [InlineData("RETIRED", true)]
    [InlineData("PUBLISHED", false)]
    public void List_status_validation_is_explicit(string? status, bool valid)
        => new ListFasSchemesRequestValidator().Validate(new ListFasSchemesRequest(status, null)).IsValid.Should().Be(valid);
}
