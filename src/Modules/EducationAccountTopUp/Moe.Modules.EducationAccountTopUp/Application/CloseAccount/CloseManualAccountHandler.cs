using System.Text.Json;
using Moe.Application.Abstractions.Audit;
using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Persistence;
using Moe.Application.Abstractions.Security;
using Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;
using Moe.Modules.EducationAccountTopUp.IGateway.Repositories;
using Moe.Modules.IdentityPlatform.IGateway.People;
using Moe.SharedKernel.Results;

namespace Moe.Modules.EducationAccountTopUp.Application.CloseAccount;

internal sealed class CloseManualAccountHandler(
    IEducationAccountRepository educationAccounts,
    IPersonDirectory people,
    ICurrentUser currentUser,
    IAdminAccessControl adminAccess,
    IClock clock,
    IUnitOfWork unitOfWork,
    IAuditService auditService,
    EducationAccountClosureEmailService closureEmails) : ICommandHandler<CloseManualAccountCommand, CloseManualAccountResponse>
{
    public async Task<Result<CloseManualAccountResponse>> Handle(
        CloseManualAccountCommand command,
        CancellationToken cancellationToken)
    {
        EducationAccount? account = await educationAccounts.FindByIdAsync(command.EducationAccountId, cancellationToken);
        if (account is null)
        {
            return Result<CloseManualAccountResponse>.Failure(EducationAccountErrors.NotFound);
        }

        PersonSummary? person = await people.FindAsync(account.PersonId, cancellationToken);
        if (person is null)
        {
            return Result<CloseManualAccountResponse>.Failure(AccountErrors.InvalidPerson);
        }

        if (person.OrganizationId is long organizationId)
        {
            Result access = adminAccess.EnsureCanAccessOrganization(organizationId);
            if (access.IsFailure)
            {
                return Result<CloseManualAccountResponse>.Failure(access.Error);
            }
        }
        else if (!adminAccess.IsHqAdmin)
        {
            return Result<CloseManualAccountResponse>.Failure(AccountErrors.OrganizationOutsideScope);
        }

        if (currentUser.UserAccountId is not long actorId)
        {
            return Result<CloseManualAccountResponse>.Failure(AccountErrors.ActorRequired);
        }

        Result closeResult = account.CloseManual(
            clock.UtcNow,
            command.ReasonCode,
            command.Remarks ?? string.Empty,
            actorId);

        if (closeResult.IsFailure)
        {
            return Result<CloseManualAccountResponse>.Failure(closeResult.Error);
        }

        string detailsJson = JsonSerializer.Serialize(new
        {
            reasonCode = command.ReasonCode,
            closedByLoginAccountId = actorId
        });

        await auditService.RecordAsync(
            AuditActionCodes.EducationAccountClosedManually,
            "EducationAccount",
            account.Id.ToString(),
            detailsJson,
            cancellationToken);

        if (person.OrganizationId is long schoolOrganizationId)
        {
            await auditService.RecordSchoolActionAsync(
                new SchoolAuditContext(
                    AuditActionCodes.EducationAccountClosedManually,
                    "EducationAccount",
                    account.Id,
                    schoolOrganizationId,
                    new SchoolAuditDetails(
                        "Education account closed manually",
                        RelatedIds: new Dictionary<string, long>
                        {
                            ["studentPersonId"] = account.PersonId
                        },
                        StatusTransition: new SchoolAuditStatusTransition(null, account.StatusCode),
                        ReasonCode: command.ReasonCode)),
                cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        string closureReason = string.IsNullOrWhiteSpace(command.Remarks)
            ? command.ReasonCode
            : $"{command.ReasonCode} - {command.Remarks.Trim()}";
        await closureEmails.SendClosedAsync(account, closureReason, cancellationToken);

        return Result<CloseManualAccountResponse>.Success(new CloseManualAccountResponse(
            account.PersonId,
            account.Id,
            account.StatusCode,
            account.ClosedAtUtc));
    }
}
