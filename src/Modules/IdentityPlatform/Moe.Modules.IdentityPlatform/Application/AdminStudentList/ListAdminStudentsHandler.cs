using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Security;
using Moe.Modules.IdentityPlatform.Application;
using Moe.Modules.IdentityPlatform.IGateway.Students;
using Moe.SharedKernel.Results;

namespace Moe.Modules.IdentityPlatform.Application.AdminStudentList;

internal sealed class ListAdminStudentsHandler(
    IAdminStudentListReader students,
    IAdminAccessControl adminAccess,
    IClock clock) : IQueryHandler<ListAdminStudentsQuery, AdminStudentListPage>
{
    public async Task<Result<AdminStudentListPage>> Handle(
        ListAdminStudentsQuery query,
        CancellationToken cancellationToken)
    {
        DateOnly today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        AdminOrganizationScope organizationScope = adminAccess.ResolveOrganizationFilter(query.OrganizationId);
        if (!organizationScope.HasAccess)
        {
            return Result<AdminStudentListPage>.Failure(IdentityErrors.OrganizationOutsideScope);
        }

        AdminStudentListCriteria criteria = new(
            organizationScope.OrganizationId,
            query.Search,
            query.LevelCodes,
            query.ClassCode,
            query.AccountStatus,
            query.EnrollmentStatus,
            Math.Max(query.Page, 1),
            Math.Clamp(query.PageSize, 1, 100));

        AdminStudentListPage page = await students.ListAsync(
            criteria,
            organizationScope.ScopedOrganizationIds,
            organizationScope.HasGlobalAccess,
            today,
            cancellationToken);

        return Result<AdminStudentListPage>.Success(page);
    }
}
