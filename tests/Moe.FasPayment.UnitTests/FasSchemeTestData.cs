using Moe.Modules.FasPayment.Contracts.AdminFasSchemes;

namespace Moe.FasPayment.UnitTests;

internal static class FasSchemeTestData
{
    public static CreateFasSchemeRequest ValidRequest() => new(
        "MOE-FAS-2026",
        "FAS-2026",
        "MOE FAS 2026",
        null,
        DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
        DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1)),
        [101],
        "PERCENTAGE",
        [new("AGE", "AND", 1), new("GHI", "AND", 2), new("PCI", "AND", 3), new("NATIONALITY", null, 4)],
        [new("Full", 100, 1, [new(1, 16, 18, null), new(2, 0, 3000, null), new(3, 0, 1000, null), new(4, null, null, ["Singapore Citizen"])])]);
}
