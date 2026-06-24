using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Security;
using Moe.Modules.IdentityPlatform.IGateway.Students;
using Moe.SharedKernel.Results;

namespace Moe.Modules.IdentityPlatform.Application.AdminStudentList;

internal sealed class ListAdminStudentClassesHandler(
    IAdminStudentListReader students,
    IAdminAccessControl adminAccess,
    IClock clock) : IQueryHandler<ListAdminStudentClassesQuery, IReadOnlyList<string>>
{
    public async Task<Result<IReadOnlyList<string>>> Handle(
        ListAdminStudentClassesQuery query,
        CancellationToken cancellationToken)
    {
        Result scope = adminAccess.EnsureCanAccessOrganization(query.OrganizationId);
        if (scope.IsFailure)
        {
            return Result<IReadOnlyList<string>>.Failure(scope.Error);
        }

        DateOnly today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        IReadOnlyList<string> classes = await students.ListClassesAsync(
            query.OrganizationId,
            query.LevelCode,
            today,
            cancellationToken);

        return Result<IReadOnlyList<string>>.Success(classes);
    }
}
