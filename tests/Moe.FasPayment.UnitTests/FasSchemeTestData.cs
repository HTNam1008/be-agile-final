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
        [],
        [
            new CreateFasTierRequest(
                "Full", 
                "PERCENTAGE", 
                100, 
                1, 
                [
                    new FasTierCriteriaRequest("AGE", 13, 18, null, "AND", 1),
                    new FasTierCriteriaRequest("NATIONALITY", null, null, ["Singapore Citizen"], null, 2)
                ]
            )
        ]
    );
}
