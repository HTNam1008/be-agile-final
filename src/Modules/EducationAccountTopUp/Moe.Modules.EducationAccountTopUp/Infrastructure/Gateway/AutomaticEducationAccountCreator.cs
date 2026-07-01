using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Moe.Application.Abstractions.Audit;
using Moe.Application.Abstractions.Persistence;
using Moe.Modules.EducationAccountTopUp.Application.OpenAccount;
using Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;
using Moe.Modules.EducationAccountTopUp.IGateway.Accounts;
using Moe.Modules.IdentityPlatform.IGateway.Students;
using Moe.Modules.Notifications.Domain.Notifications;
using Moe.Modules.Notifications.IGateway.Notifications;
using Moe.Modules.EducationAccountTopUp.IGateway.Repositories;
using Moe.SharedKernel.Results;
using Microsoft.Extensions.Logging;

namespace Moe.Modules.EducationAccountTopUp.Infrastructure.Gateway;

internal sealed class AutomaticEducationAccountCreator(
    IEducationAccountRepository educationAccounts,
    IAuditService auditService,
    IUnitOfWork unitOfWork,
    EducationAccountCreatedEmailService accountCreatedEmails,
    IStudentNotificationRecipientResolver notificationRecipients,
    INotificationWriter notificationWriter,
    ILogger<AutomaticEducationAccountCreator> logger) : IAutomaticEducationAccountCreator
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

        await accountCreatedEmails.SendAsync(result.Value, cancellationToken);
        await NotifyAccountCreatedAsync(result.Value, cancellationToken);

        return new AutomaticEducationAccountCreationResult(
            result.Value.Id,
            result.Value.AccountNumber,
            Created: true);
    }

    private async Task NotifyAccountCreatedAsync(EducationAccount account, CancellationToken cancellationToken)
    {
        long? userAccountId = await notificationRecipients.FindUserAccountIdByPersonIdAsync(account.PersonId, cancellationToken);
        if (userAccountId is null)
        {
            logger.LogWarning(
                "Skipping ACC_OPENED notification for education account {EducationAccountId}; no user account found for person {PersonId}",
                account.Id,
                account.PersonId);
            return;
        }

        Result<long> create = await notificationWriter.CreateAsync(
            new NotificationCreateRequest(
                userAccountId.Value,
                NotificationTypeCode.AccOpened,
                $"Account Opened: {account.AccountNumber}",
                "Reason: Automatic creation when the account holder became eligible."),
            cancellationToken);

        if (create.IsFailure)
        {
            logger.LogWarning(
                "Failed to create ACC_OPENED notification for user account {UserAccountId} in education account {EducationAccountId}: {ErrorCode}",
                userAccountId.Value,
                account.Id,
                create.Error.Code);
        }
    }
}
