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

namespace Moe.Modules.EducationAccountTopUp.Application.OpenAccount;

internal sealed class OpenManualAccountHandler(
    IEducationAccountRepository educationAccounts,
    IPersonDirectory people,
    ICurrentUser currentUser,
    IClock clock,
    IUnitOfWork unitOfWork,
    IAuditService auditService,
    EducationAccountCreatedEmailService accountCreatedEmails) : ICommandHandler<OpenManualAccountCommand, OpenManualAccountResponse>
{
    public async Task<Result<OpenManualAccountResponse>> Handle(
        OpenManualAccountCommand command,
        CancellationToken cancellationToken)
    {
        PersonSummary? person = await people.FindAsync(command.PersonId, cancellationToken);
        if (person is null)
        {
            return Result<OpenManualAccountResponse>.Failure(AccountErrors.InvalidPerson);
        }

        if (await educationAccounts.ExistsForPersonAsync(command.PersonId, cancellationToken))
        {
            return Result<OpenManualAccountResponse>.Failure(AccountErrors.DuplicatePersonAccount);
        }

        if (currentUser.UserAccountId is not long actorId)
        {
            return Result<OpenManualAccountResponse>.Failure(
                Moe.Modules.EducationAccountTopUp.Domain.TopUps.TopUpErrors.ActorRequired);
        }

        string accountNumber = $"EA-{clock.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}"[..25].ToUpperInvariant();
        Result<EducationAccount> accountResult = EducationAccount.OpenManual(
            command.PersonId,
            accountNumber,
            clock.UtcNow,
            command.ReasonCode,
            command.Remarks,
            actorId);

        if (accountResult.IsFailure)
        {
            return Result<OpenManualAccountResponse>.Failure(accountResult.Error);
        }

        await educationAccounts.AddAsync(accountResult.Value, cancellationToken);
        string detailsJson = JsonSerializer.Serialize(new
        {
            personId = command.PersonId,
            accountNumber = accountResult.Value.AccountNumber,
            openedByUserId = actorId
        });

        await auditService.RecordAsync(
            AuditActionCodes.EducationAccountCreatedManually,
            "EducationAccount",
            accountResult.Value.Id.ToString(),
            detailsJson,
            cancellationToken);

        if (person.OrganizationId is long schoolOrganizationId)
        {
            await auditService.RecordSchoolActionAsync(
                new SchoolAuditContext(
                    AuditActionCodes.EducationAccountCreatedManually,
                    "EducationAccount",
                    accountResult.Value.Id,
                    schoolOrganizationId,
                    new SchoolAuditDetails(
                        "Education account opened manually",
                        RelatedIds: new Dictionary<string, long>
                        {
                            ["studentPersonId"] = command.PersonId
                        },
                        ReasonCode: command.ReasonCode)),
                cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await accountCreatedEmails.SendAsync(accountResult.Value, cancellationToken);

        OpenManualAccountResponse response = new(
            accountResult.Value.Id,
            accountResult.Value.AccountNumber,
            accountResult.Value.StatusCode);

        return Result<OpenManualAccountResponse>.Success(response);
    }
}
