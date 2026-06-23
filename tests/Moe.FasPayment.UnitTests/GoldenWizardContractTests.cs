using System.Text.Json;
using FluentAssertions;
using Moe.Modules.FasPayment.Application.AdminFasSchemes;
using Moe.Modules.FasPayment.Contracts.AdminFasSchemes;
using Xunit;

namespace Moe.FasPayment.UnitTests;

public sealed class GoldenWizardContractTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData("fas-wizard-percentage-global.json", "PERCENTAGE", 0, 2)]
    [InlineData("fas-wizard-fixed-courses.json", "FIXED", 2, 1)]
    public void Golden_wizard_payload_is_a_valid_canonical_v4_request(
        string fixture,
        string subsidyType,
        int courseCount,
        int tierCount)
    {
        CreateFasSchemeRequest request = Load(fixture);

        request.Tiers[0].SubsidyType.Should().Be(subsidyType);
        request.CourseIds.Should().HaveCount(courseCount);
        request.Tiers.Should().HaveCount(tierCount);
        new CreateFasSchemeRequestValidator().Validate(request).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("fas-wizard-percentage-global.json")]
    [InlineData("fas-wizard-fixed-courses.json")]
    public void Golden_wire_payload_omits_frontend_ids_and_per_tier_scheme_fields(string fixture)
    {
        using JsonDocument document = JsonDocument.Parse(ReadFixture(fixture));

        foreach (JsonElement tier in document.RootElement.GetProperty("tiers").EnumerateArray())
        {
            tier.TryGetProperty("id", out _).Should().BeFalse();
            tier.TryGetProperty("grantCode", out _).Should().BeFalse();
            foreach (JsonElement value in tier.GetProperty("criteria").EnumerateArray())
                value.TryGetProperty("id", out _).Should().BeFalse();
        }
    }

    private static CreateFasSchemeRequest Load(string fixture)
        => JsonSerializer.Deserialize<CreateFasSchemeRequest>(ReadFixture(fixture), JsonOptions)!;

    private static string ReadFixture(string fixture)
        => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", fixture));
}
