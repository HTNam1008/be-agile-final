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
                true);
        }

        string accountNumber = $"PSEA-{personId:D8}";
        Result<EducationAccount> result = EducationAccount.OpenManual(
            personId,
            accountNumber,
            openedAtUtc,
            "SINGPASS_STUDENT_PROVISIONING",
            "Created automatically when the student Singpass account was provisioned.",
            openedByUserAccountId);

        if (result.IsFailure)
        {
            throw new InvalidOperationException(result.Error.Message);
        }

        await educationAccounts.AddAsync(result.Value, cancellationToken);
        if (saveChanges)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return new EducationAccountProvisioningResult(
            result.Value.Id,
            result.Value.AccountNumber,
            true);
    }

    public Task<bool> HasAccountAsync(long personId, CancellationToken cancellationToken)
    {
        return educationAccounts.ExistsForPersonAsync(personId, cancellationToken);
    }
}
