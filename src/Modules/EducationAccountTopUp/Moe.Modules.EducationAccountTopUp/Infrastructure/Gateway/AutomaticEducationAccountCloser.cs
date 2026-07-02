using System.Text.Json;
using Moe.Application.Abstractions.Audit;
using Moe.Application.Abstractions.Persistence;
using Moe.Modules.EducationAccountTopUp.Application.CloseAccount;
using Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;
using Moe.Modules.EducationAccountTopUp.IGateway.Accounts;
using Moe.Modules.EducationAccountTopUp.IGateway.People;
using Moe.Modules.EducationAccountTopUp.IGateway.Repositories;
using Moe.Modules.IdentityPlatform.IGateway.People;
using Moe.Modules.IdentityPlatform.IGateway.Students;
using Moe.Modules.Notifications.Domain.Notifications;
using Moe.Modules.Notifications.IGateway.Notifications;
using Microsoft.Extensions.Logging;
using Moe.SharedKernel.Results;

namespace Moe.Modules.EducationAccountTopUp.Infrastructure.Gateway;

internal sealed class AutomaticEducationAccountCloser(
    IEducationAccountRepository educationAccounts,
    IAccountHoldRepository accountHolds,
    IEligiblePersonLookupGateway people,
    IAutomaticEducationAccountSettlementGateway settlements,
    IPersonLifecycleGateway personLifecycle,
    IAuditService auditService,
    IUnitOfWork unitOfWork,
    EducationAccountClosureEmailService closureEmails,
    IStudentNotificationRecipientResolver notificationRecipients,
    INotificationWriter notificationWriter,
    ILogger<AutomaticEducationAccountCloser> logger) : IAutomaticEducationAccountCloser
{
    private const int ClosingAge = 30;

    public async Task<AutomaticEducationAccountClosureSummary> CloseEligibleAsync(
        DateOnly today,
        DateTimeOffset closedAtUtc,
        CancellationToken cancellationToken)
    {
        IReadOnlyCollection<EducationAccount> activeAccounts =
            await educationAccounts.ListActiveAsync(cancellationToken);
        if (activeAccounts.Count == 0)
        {
            return new AutomaticEducationAccountClosureSummary(0, 0, []);
        }

        IReadOnlyCollection<long> eligiblePersonIds =
            await people.FindPersonIdsAgedAtLeastAsync(
                activeAccounts.Select(x => x.PersonId).Distinct().ToArray(),
                ClosingAge,
                today,
                cancellationToken);
        HashSet<long> eligiblePersonIdSet = eligiblePersonIds.ToHashSet();

        List<AutomaticEducationAccountClosureResult> results = [];
        foreach (EducationAccount account in activeAccounts.Where(x => eligiblePersonIdSet.Contains(x.PersonId)))
        {
            AutomaticEducationAccountClosureResult result =
                await EnsureClosedAsync(account, closedAtUtc, cancellationToken);
            results.Add(result);
        }

        return new AutomaticEducationAccountClosureSummary(
            activeAccounts.Count,
            results.Count(x => x.Closed),
            results);
    }

    public async Task<AutomaticEducationAccountClosureResult> EnsureClosedAsync(
        EducationAccount account,
        DateTimeOffset closedAtUtc,
        CancellationToken cancellationToken)
    {
        if (await accountHolds.HasPendingHoldAsync(
            account.Id,
            closedAtUtc.UtcDateTime,
            cancellationToken))
        {
            return new AutomaticEducationAccountClosureResult(
                account.Id,
                account.PersonId,
                Closed: false,
                SkipReasonCode: AutomaticEducationAccountClosureSkipReasonCodes.PendingPaymentHold);
        }

        Result<bool> closeResult = account.CloseAutomatically(closedAtUtc);
        if (closeResult.IsFailure)
        {
            throw new InvalidOperationException(closeResult.Error.Message);
        }

        if (!closeResult.Value)
        {
            return new AutomaticEducationAccountClosureResult(
                account.Id,
                account.PersonId,
                Closed: false,
                SkipReasonCode: AutomaticEducationAccountClosureSkipReasonCodes.AlreadyClosed);
        }

        await settlements.SettleRemainingBalanceAsync(account, closedAtUtc, cancellationToken);

        Result disableResult = await personLifecycle.DisableAsync(
            account.PersonId,
            closedAtUtc.UtcDateTime,
            cancellationToken);
        if (disableResult.IsFailure)
        {
            throw new InvalidOperationException(disableResult.Error.Message);
        }

        string detailsJson = JsonSerializer.Serialize(new
        {
            personId = account.PersonId,
            reasonCode = EducationAccountClosingReasonCodes.AutoAgeLimit,
            closedByLoginAccountId = (long?)null
        });

        await auditService.RecordAsync(
            AuditActionCodes.EducationAccountClosedAutomatically,
            "EducationAccount",
            account.Id.ToString(),
            detailsJson,
            cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await closureEmails.SendClosedAsync(
            account,
            "Automatic closure when the account holder reached age 30",
            cancellationToken);
        await NotifyAccountClosedAsync(account, cancellationToken);

        return new AutomaticEducationAccountClosureResult(
            account.Id,
            account.PersonId,
            Closed: true);
    }

    private async Task NotifyAccountClosedAsync(EducationAccount account, CancellationToken cancellationToken)
    {
        long? userAccountId = await notificationRecipients.FindUserAccountIdByPersonIdAsync(account.PersonId, cancellationToken);
        if (userAccountId is null)
        {
            logger.LogWarning(
                "Skipping ACC_CLOSED notification for education account {EducationAccountId}; no user account found for person {PersonId}",
                account.Id,
                account.PersonId);
            return;
        }

        string closingReason = "Automatic closure when the account holder reached age 30";
        Result<long> create = await notificationWriter.CreateAsync(
            new NotificationCreateRequest(
                userAccountId.Value,
                NotificationTypeCode.AccClosed,
                "Account Closed",
                $"Reason: {closingReason}."),
            cancellationToken);

        if (create.IsFailure)
        {
            logger.LogWarning(
                "Failed to create ACC_CLOSED notification for user account {UserAccountId} in education account {EducationAccountId}: {ErrorCode}",
                userAccountId.Value,
                account.Id,
                create.Error.Code);
        }
    }
}
