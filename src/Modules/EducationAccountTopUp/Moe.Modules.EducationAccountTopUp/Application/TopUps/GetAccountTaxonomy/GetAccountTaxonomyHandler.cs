using Moe.Application.Abstractions.Security;
using Moe.Modules.EducationAccountTopUp.Domain.TopUps;
using Moe.Modules.IdentityPlatform.IGateway.Students;
using Moe.Modules.IdentityPlatform.IGateway.Students.TopUpSearch;
using Moe.SharedKernel.Results;

namespace Moe.Modules.EducationAccountTopUp.Application.TopUps.GetAccountTaxonomy;

internal sealed class GetAccountTaxonomyHandler(
    IAdminAccessControl adminAccess,
    ITopUpStudentSearchDirectory students)
    : Moe.Application.Abstractions.Messaging.IQueryHandler<GetAccountTaxonomyQuery, GetAccountTaxonomyResponse>
{
    public async Task<Result<GetAccountTaxonomyResponse>> Handle(
        GetAccountTaxonomyQuery query,
        CancellationToken cancellationToken)
    {
        AdminOrganizationScope scope = adminAccess.ResolveOrganizationFilter(query.OrganizationId);
        if (!scope.HasAccess)
        {
            return Result<GetAccountTaxonomyResponse>.Failure(TopUpErrors.OrganizationOutsideScope);
        }

        long[] scopedOrganizationIds = scope.HasGlobalAccess ? [] : scope.ScopedOrganizationIds.ToArray();

        IReadOnlyList<AccountTaxonomyLevel> levels = await students.GetAccountTaxonomyAsync(
            scopedOrganizationIds,
            cancellationToken);

        return Result<GetAccountTaxonomyResponse>.Success(new GetAccountTaxonomyResponse(levels));
    }
}
