using Moe.Modules.FasPayment.Contracts.AdminFasSchemes;

namespace Moe.FasPayment.UnitTests;

internal static class FasSchemeTestData
{
    public static CreateFasSchemeRequest ValidRequest() => new(
        "MOE-FAS-2026",
        "FAS-2026",
        "MOE FAS 2026",
        null,
        new DateOnly(2026, 1, 1),
        new DateOnly(2026, 12, 31),
        [101],
        "PERCENTAGE",
        [new("AGE", "AND", 1), new("NATIONALITY", null, 2)],
        [new("Full", 100, 1, [new(1, 13, 18, null), new(2, null, null, ["Singapore Citizen"])])]);
}
