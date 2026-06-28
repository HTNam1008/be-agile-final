using Moe.Modules.IdentityPlatform.IGateway.Students;

namespace Moe.Modules.EducationAccountTopUp.Application.TopUps.GetAccountTaxonomy;

public sealed record GetAccountTaxonomyResponse(
    IReadOnlyList<AccountTaxonomyLevel> Levels);
