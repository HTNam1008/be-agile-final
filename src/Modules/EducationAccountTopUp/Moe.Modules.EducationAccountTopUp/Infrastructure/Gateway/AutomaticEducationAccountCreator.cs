using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Moe.Application.Abstractions.Audit;
using Moe.Application.Abstractions.Persistence;
using Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;
using Moe.Modules.EducationAccountTopUp.IGateway.Accounts;
using Moe.Modules.EducationAccountTopUp.IGateway.Repositories;
using Moe.SharedKernel.Results;

namespace Moe.Modules.EducationAccountTopUp.Infrastructure.Gateway;

internal sealed class AutomaticEducationAccountCreator(
    IEducationAccountRepository educationAccounts,
    IAuditService auditService,
    IUnitOfWork unitOfWork) : IAutomaticEducationAccountCreator
{
    public async Task<AutomaticEducationAccountCreationResult> EnsureCreatedAsync(
        long personId,
        DateTimeOffset openedAtUtc,
        CancellationToken cancellationToken)
    {
        EducationAccount? existingAccount = await educationAccounts.FindByPersonIdAsync(personId, cancellationToken);
        if (existingAccount is not null)
        {
            return new AutomaticEducationAccountCreationResult(
                existingAccount.Id,
                existingAccount.AccountNumber,
                Created: false);
        }

        string accountNumber = EducationAccountNumberFactory.ForPerson(personId);
        Result<EducationAccount> result = EducationAccount.OpenAutomatically(personId, accountNumber, openedAtUtc);
        if (result.IsFailure)
        {
            throw new InvalidOperationException(result.Error.Message);
        }

        await educationAccounts.AddAsync(result.Value, cancellationToken);

        string detailsJson = JsonSerializer.Serialize(new
        {
            personId,
            accountNumber = result.Value.AccountNumber,
            openedByUserId = (long?)null
        });

        await auditService.RecordAsync(
            AuditActionCodes.EducationAccountCreatedAutomatically,
            "EducationAccount",
            result.Value.Id.ToString(),
            detailsJson,
            cancellationToken);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (EducationAccountDuplicateExceptionDetector.IsDuplicateEducationAccount(exception))
        {
            EducationAccount? reloaded = await educationAccounts.FindByPersonIdAsync(personId, cancellationToken);
            if (reloaded is null)
            {
                throw;
            }

            return new AutomaticEducationAccountCreationResult(
                reloaded.Id,
                reloaded.AccountNumber,
                Created: false);
        }

        return new AutomaticEducationAccountCreationResult(
            result.Value.Id,
            result.Value.AccountNumber,
            Created: true);
    }
}
