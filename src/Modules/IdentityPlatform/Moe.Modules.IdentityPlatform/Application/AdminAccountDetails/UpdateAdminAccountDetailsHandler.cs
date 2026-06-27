using Moe.Application.Abstractions.Audit;
using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Persistence;
using Moe.Application.Abstractions.Security;
using Moe.Modules.IdentityPlatform.IGateway.Accounts;
using Moe.Modules.IdentityPlatform.IGateway.Repositories;
using Moe.SharedKernel.Results;

namespace Moe.Modules.IdentityPlatform.Application.AdminAccountDetails;

internal sealed class UpdateAdminAccountDetailsHandler(
    IAdminAccountDetailsRepository accountDetails,
    IEducationAccountLookupGateway educationAccounts,
    IAdminAccessControl adminAccess,
    IClock clock,
    IUnitOfWork unitOfWork,
    IAuditService audit) : ICommandHandler<UpdateAdminAccountDetailsCommand, AdminAccountDetailsResponse>
{
    public async Task<Result<AdminAccountDetailsResponse>> Handle(
        UpdateAdminAccountDetailsCommand command,
        CancellationToken cancellationToken)
    {
        DateTime utcNow = clock.UtcNow.UtcDateTime;
        DateOnly today = DateOnly.FromDateTime(utcNow);

        AdminAccountDetailsProfile? currentProfile = await accountDetails.GetAsync(command.PersonId, today, cancellationToken);
        if (currentProfile is null)
        {
            return Result<AdminAccountDetailsResponse>.Failure(IdentityErrors.PersonNotFound);
        }

        Result scope = EnsureCanAccess(currentProfile.SchoolOrganizationId);
        if (scope.IsFailure)
        {
            return Result<AdminAccountDetailsResponse>.Failure(scope.Error);
        }

        AdminAccountDetailsUpdateResult update = await accountDetails.UpdateAsync(
            command.PersonId,
            command.ClassCode,
            command.ResidentialAddress,
            command.Email,
            command.ContactNumber,
            command.ExpectedUpdatedAtUtc,
            utcNow,
            today,
            cancellationToken);

        if (update.Status == AdminAccountDetailsUpdateStatus.NotFound)
        {
            return Result<AdminAccountDetailsResponse>.Failure(IdentityErrors.PersonNotFound);
        }

        if (update.Status == AdminAccountDetailsUpdateStatus.Conflict)
        {
            return Result<AdminAccountDetailsResponse>.Failure(IdentityErrors.ProfileUpdateConflict);
        }

        if (update.Status == AdminAccountDetailsUpdateStatus.ClassEnrollmentMissing)
        {
            return Result<AdminAccountDetailsResponse>.Failure(IdentityErrors.ActiveSchoolEnrollmentRequired);
        }

        EducationAccountLookupSummary? account = await educationAccounts.FindByPersonIdAsync(command.PersonId, cancellationToken);
        if (account is null)
        {
            return Result<AdminAccountDetailsResponse>.Failure(IdentityErrors.EducationAccountNotFound);
        }

        if (currentProfile.SchoolOrganizationId is long schoolOrganizationId)
        {
            await audit.RecordSchoolActionAsync(
                new SchoolAuditContext(
                    AuditActionCodes.AccountDetailsUpdatedByAdmin,
                    "Person",
                    command.PersonId,
                    schoolOrganizationId,
                    new SchoolAuditDetails(
                        "Student profile/account details updated by admin",
                        EntityDisplayName: currentProfile.OfficialFullName,
                        ChangedFields: update.ChangedFields)),
                cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<AdminAccountDetailsResponse>.Success(
            AdminAccountDetailsMapper.ToResponse(update.Profile!, account));
    }

    private Result EnsureCanAccess(long? organizationId)
    {
        if (organizationId is null)
        {
            return adminAccess.IsHqAdmin
                ? Result.Success()
                : Result.Failure(IdentityErrors.OrganizationOutsideScope);
        }

        return adminAccess.EnsureCanAccessOrganization(organizationId.Value);
    }
}
