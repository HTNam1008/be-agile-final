using FluentAssertions;
using Moe.Modules.FasPayment.Domain.Fas;
using Xunit;

namespace Moe.FasPayment.UnitTests;

public sealed class FasSchemeDomainTests
{
    private static readonly DateTime Now = new(2026, 6, 22, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void CreateDraft_requires_codes_name_and_valid_dates()
    {
        Action emptyGrant = () => FasScheme.CreateDraft("S", " ", "Name", null, Today().AddDays(1), Today().AddMonths(6), 1, Now);
        Action reversed = () => FasScheme.CreateDraft("S", "G", "Name", null, Today().AddMonths(6), Today().AddDays(1), 1, Now);
        Action equalDates = () => FasScheme.CreateDraft("S", "G", "Name", null, Today().AddDays(1), Today().AddDays(1), 1, Now);
        Action pastStart = () => FasScheme.CreateDraft("S", "G", "Name", null, Today().AddDays(-1), Today().AddMonths(6), 1, Now);
        emptyGrant.Should().Throw<ArgumentException>();
        reversed.Should().Throw<ArgumentException>();
        equalDates.Should().Throw<ArgumentException>().WithMessage("*End date must be after start date.*");
        pastStart.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateDraft_uses_singapore_business_day_for_start_date()
    {
        DateTime utcNow = new(2026, 6, 30, 16, 30, 0, DateTimeKind.Utc);

        Action utcDayStart = () => FasScheme.CreateDraft(
            "S-SGT",
            "G-SGT",
            "Singapore day scheme",
            null,
            new DateOnly(2026, 6, 30),
            new DateOnly(2026, 8, 1),
            1,
            utcNow);
        utcDayStart.Should().Throw<ArgumentException>().WithMessage("*Start date cannot be before today.*");

        FasScheme scheme = FasScheme.CreateDraft(
            "S-SGT",
            "G-SGT",
            "Singapore day scheme",
            null,
            new DateOnly(2026, 7, 1),
            new DateOnly(2026, 8, 1),
            1,
            utcNow);

        scheme.StartDate.Should().Be(new DateOnly(2026, 7, 1));
    }

    [Fact]
    public void Scheme_follows_draft_active_retired_lifecycle()
    {
        FasScheme scheme = FasScheme.CreateDraft("S", "G", "Name", null, Today().AddDays(1), Today().AddMonths(6), 1, Now);
        scheme.Activate(2, Now.AddMinutes(1));
        scheme.StatusCode.Should().Be("ACTIVE");
        Action activateTwice = () => scheme.Activate(2, Now);
        activateTwice.Should().Throw<InvalidOperationException>();
        scheme.Retire(3, Now.AddMinutes(2));
        scheme.StatusCode.Should().Be("RETIRED");
    }

    [Theory]
    [InlineData("PERCENTAGE", 100)]
    [InlineData("FIXED", 0)]
    public void Tier_accepts_valid_subsidies(string type, decimal value)
        => FasTier.Create(1, "Tier", type, value, 1, Now).SubsidyValue.Should().Be(value);

    [Fact]
    public void Tier_rejects_percentage_over_100_and_negative_fixed()
    {
        ((Action)(() => FasTier.Create(1, "Tier", "PERCENTAGE", 101, 1, Now))).Should().Throw<ArgumentOutOfRangeException>();
        ((Action)(() => FasTier.Create(1, "Tier", "FIXED", -1, 1, Now))).Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Criteria_enforces_numeric_and_nationality_shapes()
    {
        FasTierCriteria.Create(1, "AGE", 13, 18, null, 1, Now).CriteriaType.Should().Be("AGE");
        FasTierCriteria.Create(1, "NATIONALITY", null, null, null, 1, Now).CriteriaType.Should().Be("NATIONALITY");
        ((Action)(() => FasTierCriteria.Create(1, "AGE", null, 18, null, 1, Now))).Should().Throw<ArgumentException>();
        ((Action)(() => FasTierCriteria.Create(1, "NATIONALITY", 1, null, null, 1, Now))).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Rejection_requires_a_reason()
    {
        ((Action)(() => FasApplicationReviewDecision.CreateRejection(1, 2, " ", null, Now))).Should().Throw<ArgumentException>();
        FasApplicationReviewDecision.CreateApproval(1, 2, null, Now).Decision.Should().Be("APPROVED");
    }

    private static DateOnly Today() => DateOnly.FromDateTime(Now);
}
