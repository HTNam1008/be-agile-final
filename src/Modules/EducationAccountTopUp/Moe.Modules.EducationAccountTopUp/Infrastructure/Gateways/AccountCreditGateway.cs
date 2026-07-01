using System.Globalization;
using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moe.Application.Abstractions.Clock;
using Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;
using Moe.Modules.EducationAccountTopUp.Domain.TopUps;
using Moe.Modules.EducationAccountTopUp.IGateway;
using Moe.Modules.IdentityPlatform.Domain.People;
using Moe.Modules.MailDelivery.IGateway;
using Moe.Modules.MailDelivery.Templates;
using Moe.SharedKernel.Results;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.EducationAccountTopUp.Infrastructure.Gateways;

/// <summary>
/// Temporarily hosts ledger posting for top-up credits until the Account module exposes its service.
/// TODO: Replace this adapter with a cross-module Account credit service when it is available.
/// </summary>
internal sealed class AccountCreditGateway(
    MoeDbContext dbContext,
    IClock clock,
    ITopUpExecutionMetrics metrics,
    IEmailNotificationQueue mailQueue,
    IEmailDeliverySwitch mailSwitch,
    IEmailBrandingProvider branding,
    ILogger<AccountCreditGateway> logger) : IAccountCreditGateway
{
    private const string CreditTransactionTypeCode = "CREDIT";
    private const string TopUpReferenceTypeCode = "TOPUP";

    public async Task<Result<CreditAccountResult>> CreditAccountForTopUpAsync(
        long educationAccountId,
        decimal amount,
        string idempotencyKey,
        string reason,
        CancellationToken cancellationToken = default)
    {
        Result<CreditAccountResult>? existing = await TryGetExistingCreditAsync(
            idempotencyKey,
            cancellationToken);

        if (existing is not null)
        {
            return existing;
        }

        if (amount <= 0)
        {
            return Result<CreditAccountResult>.Failure(TopUpErrors.InvalidCreditAmount);
        }

        EducationAccount? account = await dbContext.Set<EducationAccount>()
            .FirstOrDefaultAsync(x => x.Id == educationAccountId, cancellationToken);

        if (account is null)
        {
            return Result<CreditAccountResult>.Failure(TopUpErrors.AccountNotFound);
        }

        if (account.StatusCode != AccountStatuses.Active)
        {
            return Result<CreditAccountResult>.Failure(TopUpErrors.AccountNotActive);
        }

        AccountTransaction transaction = AccountTransaction.Create(
            educationAccountId: educationAccountId,
            transactionTypeCode: CreditTransactionTypeCode,
            amount: amount,
            referenceTypeCode: TopUpReferenceTypeCode,
            referenceId: null,
            idempotencyKey: idempotencyKey,
            currentBalance: account.CachedBalance,
            description: reason,
            createdByUserId: null,
            nowUtc: clock.UtcNow.UtcDateTime);

        dbContext.Set<AccountTransaction>().Add(transaction);
        account.UpdateBalance(amount);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            metrics.RecordAccountCreditDbConflict();

            Result<CreditAccountResult>? raceResult = await TryGetExistingCreditAsync(
                idempotencyKey,
                cancellationToken);

            if (raceResult is not null)
            {
                return raceResult;
            }

            throw;
        }

        logger.LogInformation(
            "Credited account {EducationAccountId} with {Amount}; AccountTransactionId={AccountTransactionId}",
            educationAccountId,
            amount,
            transaction.Id);

        await SendTopUpCreditedEmailAsync(
            account,
            amount,
            reason,
            account.CachedBalance,
            transaction.TransactionAtUtc,
            cancellationToken);

        return Result<CreditAccountResult>.Success(new CreditAccountResult
        {
            AccountTransactionId = transaction.Id,
            AlreadyProcessed = false
        });
    }

    private async Task<Result<CreditAccountResult>?> TryGetExistingCreditAsync(
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        AccountTransaction? existingTransaction = await dbContext.Set<AccountTransaction>()
            .FirstOrDefaultAsync(x => x.IdempotencyKey == idempotencyKey, cancellationToken);

        if (existingTransaction is null)
        {
            return null;
        }

        logger.LogInformation(
            "Credit already processed for idempotency key {IdempotencyKey}; AccountTransactionId={AccountTransactionId}",
            idempotencyKey,
            existingTransaction.Id);

        return Result<CreditAccountResult>.Success(new CreditAccountResult
        {
            AccountTransactionId = existingTransaction.Id,
            AlreadyProcessed = true
        });
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        string? message = exception.InnerException?.Message ?? exception.Message;

        return message.Contains("unique", StringComparison.OrdinalIgnoreCase)
            || message.Contains("duplicate", StringComparison.OrdinalIgnoreCase)
            || message.Contains("IX_AccountTransaction_IdempotencyKey", StringComparison.OrdinalIgnoreCase);
    }

    private async Task SendTopUpCreditedEmailAsync(
        EducationAccount account,
        decimal amount,
        string campaignReason,
        decimal updatedBalance,
        DateTime creditedAtUtc,
        CancellationToken cancellationToken)
    {
        if (!mailSwitch.IsEnabled)
        {
            logger.LogInformation(
                "Top-up email skipped because MailDelivery is disabled. PersonId={PersonId} EducationAccountId={EducationAccountId}",
                account.PersonId,
                account.Id);
            return;
        }

        Person? person = await dbContext.Set<Person>()
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == account.PersonId, cancellationToken);

        string studentName = string.IsNullOrWhiteSpace(person?.OfficialFullName)
            ? "Student"
            : person.OfficialFullName.Trim();
        string amountDisplay = FormatMoney(amount);
        string updatedBalanceDisplay = FormatMoney(updatedBalance);
        string campaignDisplay = string.IsNullOrWhiteSpace(campaignReason)
            ? "Government top-up"
            : campaignReason.Trim();
        string creditedDateDisplay = creditedAtUtc.ToString("dd MMM yyyy, HH:mm 'UTC'", CultureInfo.InvariantCulture);

        const string subject = "Funds Credited to Your Education Account";
        string plainTextBody = string.Join(Environment.NewLine, [
            branding.AppName,
            "Education Account top-up",
            string.Empty,
            $"Hello {studentName},",
            string.Empty,
            $"{amountDisplay} has been credited to your Education Account.",
            string.Empty,
            $"Reason/Campaign: {campaignDisplay}",
            $"Date Credited: {creditedDateDisplay}",
            string.Empty,
            $"Updated Balance: {updatedBalanceDisplay}",
            string.Empty,
            $"View My Account -> {branding.AccountPortalUrl}"
        ]);

        string htmlBody = BuildTopUpCreditedHtmlBody(
            studentName,
            amountDisplay,
            campaignDisplay,
            creditedDateDisplay,
            updatedBalanceDisplay,
            branding.AppName,
            branding.AccountPortalUrl);

        try
        {
            Result result = await mailQueue.EnqueueAsync(
                EmailNotificationJob.ForPerson(
                    "NOTI-02",
                    account.PersonId,
                    subject,
                    plainTextBody,
                    htmlBody,
                    "EducationAccount",
                    account.Id.ToString(CultureInfo.InvariantCulture)),
                cancellationToken);

            if (result.IsFailure)
            {
                logger.LogWarning(
                    "Top-up credited email enqueue failed. EducationAccountId={EducationAccountId} AccountTransactionDate={CreditedAtUtc} ErrorCode={ErrorCode}",
                    account.Id,
                    creditedAtUtc,
                    result.Error.Code);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(
                ex,
                "Top-up credited email notification threw an exception. EducationAccountId={EducationAccountId} AccountTransactionDate={CreditedAtUtc}",
                account.Id,
                creditedAtUtc);
        }
    }

    private static string BuildTopUpCreditedHtmlBody(
        string studentName,
        string amountDisplay,
        string campaignDisplay,
        string creditedDateDisplay,
        string updatedBalanceDisplay,
        string appName,
        string accountPortalUrl)
    {
        string encodedStudentName = WebUtility.HtmlEncode(studentName);
        string encodedAmount = WebUtility.HtmlEncode(amountDisplay);
        string encodedCampaign = WebUtility.HtmlEncode(campaignDisplay);
        string encodedCreditedDate = WebUtility.HtmlEncode(creditedDateDisplay);
        string encodedUpdatedBalance = WebUtility.HtmlEncode(updatedBalanceDisplay);

        StringBuilder builder = new();
        EmailTemplateBranding.AppendShellStart(builder);
        EmailTemplateBranding.AppendHeader(builder, "Funds credited to your Education Account", appName);
        builder.Append("<tr><td style=\"padding:30px;\">");
        builder.Append("<p style=\"font-size:16px;line-height:24px;margin:0 0 16px;color:#172033;\">Hello ")
            .Append(encodedStudentName)
            .Append(",</p>");
        builder.Append("<p style=\"font-size:16px;line-height:24px;margin:0 0 22px;color:#172033;\"><strong>")
            .Append(encodedAmount)
            .Append("</strong> has been credited to your Education Account.</p>");
        builder.Append("<table role=\"presentation\" width=\"100%\" border=\"0\" cellspacing=\"0\" cellpadding=\"0\" style=\"border-collapse:collapse;margin:0 0 24px;\">");
        AppendSummaryRow(builder, "Amount Credited", encodedAmount, EmailTemplateBranding.PrimarySoftColor, EmailTemplateBranding.PrimaryTextColor);
        AppendSummaryRow(builder, "Updated Balance", encodedUpdatedBalance, "#f8fafc", "#334155");
        AppendSummaryRow(builder, "Reason/Campaign", encodedCampaign, "#f8fafc", "#334155");
        AppendSummaryRow(builder, "Date Credited", encodedCreditedDate, "#fff7ed", "#9a3412");
        builder.Append("</table>");
        EmailTemplateBranding.AppendButton(builder, accountPortalUrl, "View My Account");
        builder.Append("</td></tr>");
        EmailTemplateBranding.AppendFooter(builder, $"This message was sent by {appName} after a completed Education Account top-up.");
        return builder.ToString();
    }

    private static void AppendSummaryRow(
        StringBuilder builder,
        string label,
        string value,
        string backgroundColor,
        string valueColor)
    {
        builder.Append("<tr><td bgcolor=\"")
            .Append(backgroundColor)
            .Append("\" style=\"background-color:")
            .Append(backgroundColor)
            .Append(";padding:14px 16px;border-bottom:8px solid #ffffff;\">");
        builder.Append("<div style=\"font-size:12px;line-height:18px;color:#64748b;text-transform:uppercase;font-weight:bold;letter-spacing:1px;\">")
            .Append(WebUtility.HtmlEncode(label))
            .Append("</div>");
        builder.Append("<div style=\"font-size:22px;line-height:28px;color:")
            .Append(valueColor)
            .Append(";font-weight:bold;padding-top:4px;\">")
            .Append(value)
            .Append("</div>");
        builder.Append("</td></tr>");
    }

    private static string FormatMoney(decimal amount) => $"SGD {amount:N2}";
}
