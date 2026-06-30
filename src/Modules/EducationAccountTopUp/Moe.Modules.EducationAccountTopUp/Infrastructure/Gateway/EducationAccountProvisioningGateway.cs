using Microsoft.EntityFrameworkCore;
using Moe.Application.Abstractions.Persistence;
using Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;
using Moe.Modules.EducationAccountTopUp.IGateway.Repositories;
using Moe.Modules.IdentityPlatform.IGateway.Accounts;
using Moe.SharedKernel.Results;

namespace Moe.Modules.EducationAccountTopUp.Infrastructure.Gateway;

internal sealed class EducationAccountProvisioningGateway(
    IEducationAccountRepository educationAccounts,
    IUnitOfWork unitOfWork) : IEducationAccountProvisioningGateway
{
    public async Task<EducationAccountProvisioningResult> EnsureAccountForStudentAsync(
        long personId,
        long openedByUserAccountId,
        DateTimeOffset openedAtUtc,
        CancellationToken cancellationToken,
        bool saveChanges = true)
    {
        EducationAccount? existingAccount = await educationAccounts.FindByPersonIdAsync(personId, cancellationToken);

        if (existingAccount is not null)
        {
            return new EducationAccountProvisioningResult(
                existingAccount.Id,
                existingAccount.AccountNumber,
                true)
            {
                Created = false
            };
        }

        string accountNumber = EducationAccountNumberFactory.ForPerson(personId);
        Result<EducationAccount> result = EducationAccount.OpenAutomatically(
            personId,
            accountNumber,
            openedAtUtc);

        if (result.IsFailure)
        {
            throw new InvalidOperationException(result.Error.Message);
        }

        await educationAccounts.AddAsync(result.Value, cancellationToken);
        if (saveChanges)
        {
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

                return new EducationAccountProvisioningResult(
                    reloaded.Id,
                    reloaded.AccountNumber,
                    true)
                {
                    Created = false
                };
            }
        }

        return new EducationAccountProvisioningResult(
            result.Value.Id,
            result.Value.AccountNumber,
            true)
        {
            Created = true
        };
    }

    public Task<bool> HasAccountAsync(long personId, CancellationToken cancellationToken)
    {
        return educationAccounts.ExistsForPersonAsync(personId, cancellationToken);
    }
}
