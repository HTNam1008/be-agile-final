using FluentAssertions;
using Moe.Modules.FasPayment.Application.StudentApplications;
using Moe.Modules.FasPayment.Domain.Fas;
using Xunit;

namespace Moe.FasPayment.UnitTests;

public sealed class FasBenefitRecommendationServiceTests
{
    private readonly FasBenefitRecommendationService _service = new();

    [Theory]
    [InlineData(1000, true)]
    [InlineData(1500, true)]
    [InlineData(2000, true)]
    [InlineData(999.99, false)]
    [InlineData(2000.01, false)]
    public void Recommend_UsesInclusivePciBounds(decimal pci, bool expectedMatch)
    {
        FasRecommendationResult result = Recommend(
            [Scheme(1)],
            [Tier(1, 1, "PCI Tier", 100, 1)],
            [],
            [Criteria(1, 1, "PCI", 1000, 2000, null, 1)],
            [],
            Facts(pci: pci));

        result.MatchedSchemes.Any().Should().Be(expectedMatch);
        result.RecommendationStatus.Should().Be(expectedMatch
            ? FasBenefitRecommendationService.Matched
            : FasBenefitRecommendationService.NoMatchAdminReview);
    }

    [Fact]
    public void Recommend_FailsWhenPciIsMissingForPciCriteria()
    {
        FasRecommendationResult result = Recommend(
            [Scheme(1)],
            [Tier(1, 1, "PCI Tier", 100, 1)],
            [],
            [Criteria(1, 1, "PCI", 0, 1000, null, 1)],
            [],
            Facts(pci: null));

        result.MatchedSchemes.Should().BeEmpty();
        result.RecommendationStatus.Should().Be(FasBenefitRecommendationService.NoMatchAdminReview);
        result.ManualReviewReason.Should().Be(FasBenefitRecommendationService.NoCriteriaMatch);
    }

    [Fact]
    public void Recommend_RequiresAllCriteriaInsideAGroup()
    {
        FasRecommendationResult result = Recommend(
            [Scheme(1)],
            [Tier(1, 1, "Strict", 100, 1)],
            [Group(10, 1, 1)],
            [
                Criteria(1, 1, "GHI", 0, 3000, null, 1, 10),
                Criteria(2, 1, "PCI", 0, 1000, null, 2, 10)
            ],
            [],
            Facts(ghi: 2500, pci: 1200));

        result.MatchedSchemes.Should().BeEmpty();
        result.RecommendationStatus.Should().Be(FasBenefitRecommendationService.NoMatchAdminReview);
    }

    [Fact]
    public void Recommend_AllowsEitherCompleteOrGroupToMatch()
    {
        FasRecommendationResult result = Recommend(
            [Scheme(1)],
            [Tier(1, 1, "Flexible", 100, 1)],
            [Group(10, 1, 1), Group(20, 1, 2)],
            [
                Criteria(1, 1, "GHI", 0, 3000, null, 1, 10),
                Criteria(2, 1, "PCI", 0, 1000, null, 2, 10),
                Criteria(3, 1, "PARENT_NATIONALITY", null, null, null, 3, 20),
                Criteria(4, 1, "ACCOUNT_TYPE", null, null, null, 4, 20)
            ],
            [
                Category(3, "PARENT_NATIONALITY", "Permanent Resident"),
                Category(4, "ACCOUNT_TYPE", "EDUCATION_ACCOUNT")
            ],
            Facts(ghi: 9000, pci: 4000, parentNationalities: ["Permanent Resident"], accountType: "EDUCATION_ACCOUNT"));

        result.MatchedSchemes.Should().ContainSingle();
        result.Recommended!.TierLabel.Should().Be("Flexible");
    }

    [Fact]
    public void Recommend_RanksHigherSubsidyBeforeDisplayOrder()
    {
        FasRecommendationResult result = Recommend(
            [Scheme(1)],
            [Tier(1, 1, "Lower First", 50, 1), Tier(2, 1, "Better Second", 90, 2)],
            [],
            [],
            [],
            Facts());

        result.MatchedSchemes.Select(x => x.TierLabel).Should().Equal("Better Second", "Lower First");
        result.Recommended!.TierId.Should().Be(2);
    }

    [Fact]
    public void Recommend_UsesDisplayOrderWhenSubsidyTies()
    {
        FasRecommendationResult result = Recommend(
            [Scheme(1)],
            [Tier(1, 1, "Later", 75, 2), Tier(2, 1, "Earlier", 75, 1)],
            [],
            [],
            [],
            Facts());

        result.Recommended!.TierLabel.Should().Be("Earlier");
    }

    [Fact]
    public void Recommend_SortsMultipleSchemesByBestBenefit()
    {
        FasRecommendationResult result = Recommend(
            [Scheme(1, "Scheme A"), Scheme(2, "Scheme B")],
            [Tier(1, 1, "A Tier", 60, 1), Tier(2, 2, "B Tier", 95, 1)],
            [],
            [],
            [],
            Facts());

        result.MatchedSchemes.Select(x => x.SchemeName).Should().Equal("Scheme B", "Scheme A");
        result.Recommended!.SchemeId.Should().Be(2);
    }

    [Fact]
    public void Recommend_ReturnsNoOpenSchemeWhenNoSchemesAreAvailable()
    {
        FasRecommendationResult result = Recommend([], [], [], [], [], Facts());

        result.RecommendationStatus.Should().Be(FasBenefitRecommendationService.NoOpenScheme);
        result.ManualReviewReason.Should().BeNull();
        result.MatchedSchemes.Should().BeEmpty();
    }

    private FasRecommendationResult Recommend(
        IReadOnlyCollection<FasRecommendationScheme> schemes,
        IReadOnlyCollection<FasTier> tiers,
        IReadOnlyCollection<FasTierCriteriaGroup> groups,
        IReadOnlyCollection<FasTierCriteria> criteria,
        IReadOnlyCollection<FasTierCriteriaNationality> values,
        FasRecommendationFacts facts)
        => _service.Recommend(schemes, tiers, groups, criteria, values, facts);

    private static FasRecommendationScheme Scheme(long id, string name = "Scheme")
        => new(id, name, null);

    private static FasRecommendationFacts Facts(
        decimal? age = 18,
        decimal? ghi = 3000,
        decimal? pci = 750,
        string? nationality = "Singapore Citizen",
        IReadOnlyCollection<string>? parentNationalities = null,
        string? accountType = "EDUCATION_ACCOUNT")
        => new(age, ghi, pci, nationality, parentNationalities ?? ["Singapore Citizen"], accountType);

    private static FasTier Tier(long id, long schemeId, string label, decimal value, int displayOrder)
    {
        FasTier tier = FasTier.Create(schemeId, label, FasSubsidyTypes.Percentage, value, displayOrder, DateTime.UtcNow);
        SetId(tier, id);
        return tier;
    }

    private static FasTierCriteria Criteria(
        long id,
        long tierId,
        string type,
        decimal? from,
        decimal? to,
        string? connector,
        int displayOrder,
        long groupId = 0)
        => FasTierCriteria.Create(tierId, groupId, type, from, to, connector, displayOrder, DateTime.UtcNow, id);

    private static FasTierCriteriaGroup Group(long id, long tierId, int displayOrder)
        => FasTierCriteriaGroup.Create(tierId, displayOrder, DateTime.UtcNow, id);

    private static FasTierCriteriaNationality Category(long criteriaId, string criteriaType, string value)
        => FasTierCriteriaNationality.Create(criteriaId, criteriaType, value);

    private static void SetId(object entity, long id)
    {
        entity.GetType().BaseType?.GetProperty("Id")?.SetValue(entity, id);
    }
}
