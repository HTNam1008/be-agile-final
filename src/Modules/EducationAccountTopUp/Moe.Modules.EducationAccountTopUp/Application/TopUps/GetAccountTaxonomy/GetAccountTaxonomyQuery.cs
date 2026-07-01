using Moe.Modules.IdentityPlatform.IGateway.Students.TopUpSearch;

namespace Moe.Modules.EducationAccountTopUp.Application.TopUps.GetAccountTaxonomy;

public sealed record GetAccountTaxonomyQuery(long? OrganizationId)
    : Moe.Application.Abstractions.Messaging.IQuery<GetAccountTaxonomyResponse>;
