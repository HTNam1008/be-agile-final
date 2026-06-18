using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Security;
using Moe.Modules.IdentityPlatform.Application;
using Moe.Modules.IdentityPlatform.Domain.Iam;
using Moe.Modules.IdentityPlatform.IGateway.Repositories;
using Moe.SharedKernel.Results;

namespace Moe.Modules.IdentityPlatform.Application.StudentProfile.GetMyStudentProfile;

internal sealed class GetMyStudentProfileHandler(
    ICurrentUser currentUser,
    IUserAccountRepository userAccounts,
    IStudentProfileRepository studentProfiles,
    IClock clock)
    : IQueryHandler<GetMyStudentProfileQuery, StudentProfileResponse>
{
    public async Task<Result<StudentProfileResponse>> Handle(GetMyStudentProfileQuery query, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated
            || currentUser.UserAccountId is null
            || currentUser.PersonId is null
            || currentUser.Portal != PortalAccessCodes.EService)
        {
            return Result<StudentProfileResponse>.Failure(IdentityErrors.AuthenticatedUserRequired);
        }

        UserAccount? account = await userAccounts.FindByIdAsync(currentUser.UserAccountId.Value, cancellationToken);

        if (account is null || account.PortalAccessCode != PortalAccessCodes.EService || account.RoleCode != RoleCodes.Student)
        {
            return Result<StudentProfileResponse>.Failure(IdentityErrors.UserAccountNotFound);
        }

        DateOnly today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        StudentProfileSummary? profile = await studentProfiles.GetProfileSummaryAsync(currentUser.PersonId.Value, today, cancellationToken);

        if (profile is null)
        {
            return Result<StudentProfileResponse>.Failure(IdentityErrors.PersonNotFound);
        }

        return Result<StudentProfileResponse>.Success(StudentProfileMapper.ToResponse(account, profile, today, currentUser));
    }
}
