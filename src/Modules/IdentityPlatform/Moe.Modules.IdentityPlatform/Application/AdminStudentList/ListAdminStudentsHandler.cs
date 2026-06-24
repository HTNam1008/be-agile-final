using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Security;
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
        AdminStudentListCriteria criteria = new(
            query.Search,
            query.LevelCode,
            query.ClassCode,
            query.AccountStatus,
            query.Residency,
            query.EnrollmentStatus,
            Math.Max(query.Page, 1),
            Math.Clamp(query.PageSize, 1, 100));

        AdminStudentListPage page = await students.ListAsync(
            criteria,
            adminAccess.ScopedOrganizationIds,
            adminAccess.IsHqAdmin,
            today,
            cancellationToken);

        return Result<AdminStudentListPage>.Success(page);
    }
}
