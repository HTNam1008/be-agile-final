using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Security;
using Moe.Modules.IdentityPlatform.IGateway.Accounts;
using Moe.Modules.IdentityPlatform.IGateway.Repositories;
using Moe.SharedKernel.Results;

namespace Moe.Modules.IdentityPlatform.Application.AdminAccountDetails;

internal sealed class GetAdminAccountDetailsHandler(
    IAdminAccountDetailsRepository accountDetails,
    IEducationAccountLookupGateway educationAccounts,
    IAdminAccessControl adminAccess,
    IClock clock) : IQueryHandler<GetAdminAccountDetailsQuery, AdminAccountDetailsResponse>
{
    public async Task<Result<AdminAccountDetailsResponse>> Handle(
        GetAdminAccountDetailsQuery query,
        CancellationToken cancellationToken)
    {
        DateOnly today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        AdminAccountDetailsProfile? profile = await accountDetails.GetAsync(query.PersonId, today, cancellationToken);
        if (profile is null)
        {
            return Result<AdminAccountDetailsResponse>.Failure(IdentityErrors.PersonNotFound);
        }

        Result scope = EnsureCanAccess(profile.SchoolOrganizationId);
        if (scope.IsFailure)
        {
            return Result<AdminAccountDetailsResponse>.Failure(scope.Error);
        }

        EducationAccountLookupSummary? account = await educationAccounts.FindByPersonIdAsync(query.PersonId, cancellationToken);
        if (account is null)
        {
            return Result<AdminAccountDetailsResponse>.Success(AdminAccountDetailsMapper.ToNoAccountResponse(profile));
        }

        return Result<AdminAccountDetailsResponse>.Success(AdminAccountDetailsMapper.ToResponse(profile, account));
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
