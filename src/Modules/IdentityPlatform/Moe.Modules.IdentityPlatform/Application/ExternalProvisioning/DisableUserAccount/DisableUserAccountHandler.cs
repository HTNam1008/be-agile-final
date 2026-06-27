using Moe.Application.Abstractions.Audit;
using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Persistence;
using Moe.Modules.IdentityPlatform.Application;
using Moe.Modules.IdentityPlatform.Domain.Iam;
using Moe.Modules.IdentityPlatform.IGateway.Repositories;
using Moe.SharedKernel.Results;

namespace Moe.Modules.IdentityPlatform.Application.ExternalProvisioning.DisableUserAccount;

internal sealed class DisableUserAccountHandler(
    IUserAccountRepository userAccounts,
    IStudentProfileRepository studentProfiles,
    IClock clock,
    IAuditService audit,
    IUnitOfWork unitOfWork) : ICommandHandler<DisableUserAccountCommand, DisableUserAccountResponse>
{
    public async Task<Result<DisableUserAccountResponse>> Handle(DisableUserAccountCommand command, CancellationToken cancellationToken)
    {
        UserAccount? account = await userAccounts.DisableAsync(
            command.UserAccountId,
            clock.UtcNow.UtcDateTime,
            cancellationToken);

        if (account is null)
        {
            return Result<DisableUserAccountResponse>.Failure(IdentityErrors.UserAccountNotFound);
        }

        if (account.PersonId is long personId)
        {
            DateOnly today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
            StudentProfileSummary? profile = await studentProfiles.GetProfileSummaryAsync(personId, today, cancellationToken);
            if (profile?.SchoolOrganizationId is long schoolOrganizationId)
            {
                await audit.RecordSchoolActionAsync(
                    new SchoolAuditContext(
                        AuditActionCodes.UserAccountDisabled,
                        "UserAccount",
                        account.Id,
                        schoolOrganizationId,
                        new SchoolAuditDetails(
                            "Student/user account disabled",
                            EntityDisplayName: profile.OfficialFullName,
                            RelatedIds: new Dictionary<string, long>
                            {
                                ["studentPersonId"] = personId
                            },
                            StatusTransition: new SchoolAuditStatusTransition(null, account.AccountStatusCode))),
                    cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }

        return Result<DisableUserAccountResponse>.Success(new DisableUserAccountResponse(account.Id, account.AccountStatusCode));
    }
}
