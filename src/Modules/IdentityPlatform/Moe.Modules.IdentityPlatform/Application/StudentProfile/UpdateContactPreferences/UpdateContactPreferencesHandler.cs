using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Security;
using Moe.Modules.IdentityPlatform.Application;
using Moe.Modules.IdentityPlatform.Domain.Iam;
using Moe.Modules.IdentityPlatform.IGateway.Repositories;
using Moe.SharedKernel.Results;

namespace Moe.Modules.IdentityPlatform.Application.StudentProfile.UpdateContactPreferences;

internal sealed class UpdateContactPreferencesHandler(
    ICurrentUser currentUser,
    IStudentProfileRepository studentProfiles,
    IUserAccountRepository userAccounts,
    IClock clock)
    : ICommandHandler<UpdateContactPreferencesCommand, StudentProfileResponse>
{
    public async Task<Result<StudentProfileResponse>> Handle(
        UpdateContactPreferencesCommand command,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated
            || currentUser.UserAccountId is null
            || currentUser.PersonId is null
            || currentUser.Portal != PortalAccessCodes.EService)
        {
            return Result<StudentProfileResponse>.Failure(IdentityErrors.AuthenticatedUserRequired);
        }

        DateTime utcNow = clock.UtcNow.UtcDateTime;
        UpdatePreferredContactResult update = await studentProfiles.UpdatePreferredContactAsync(
            currentUser.PersonId.Value,
            command.PreferredEmail,
            command.PreferredMobile,
            command.PreferredAddress,
            command.ExpectedUpdatedAtUtc,
            utcNow,
            cancellationToken);

        if (update.Status == UpdatePreferredContactStatus.Conflict)
        {
            return Result<StudentProfileResponse>.Failure(IdentityErrors.ProfileUpdateConflict);
        }

        if (update.Status == UpdatePreferredContactStatus.NotFound || update.Profile is null)
        {
            return Result<StudentProfileResponse>.Failure(IdentityErrors.PersonNotFound);
        }

        UserAccount? account = await userAccounts.FindByIdAsync(currentUser.UserAccountId.Value, cancellationToken);
        if (account is null || account.PortalAccessCode != PortalAccessCodes.EService || account.RoleCode != RoleCodes.Student)
        {
            return Result<StudentProfileResponse>.Failure(IdentityErrors.UserAccountNotFound);
        }

        DateOnly today = DateOnly.FromDateTime(utcNow);
        return Result<StudentProfileResponse>.Success(StudentProfileMapper.ToResponse(account, update.Profile, today, currentUser));
    }
}
