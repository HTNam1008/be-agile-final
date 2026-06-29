using System.Globalization;
using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;
using Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;
using Moe.Modules.IdentityPlatform.IGateway.People;
using Moe.Modules.MailDelivery.IGateway;
using Moe.Modules.MailDelivery.Templates;
using Moe.SharedKernel.Results;

namespace Moe.Modules.EducationAccountTopUp.Application.CloseAccount;

internal sealed class EducationAccountClosureEmailService(
    IPersonDirectory people,
    IEmailRecipientResolver recipientResolver,
    IEmailDeliveryGateway mailGateway,
    ILogger<EducationAccountClosureEmailService> logger)
{
    private const string PaymentDashboardUrl = "http://localhost:5173/portal/payments";

    public async Task SendClosedAsync(
        EducationAccount account,
        string reason,
        CancellationToken cancellationToken)
    {
        EmailRecipient? recipient = await ResolveRecipientAsync(account, "closed", cancellationToken);
        if (recipient is null)
        {
            return;
        }

        PersonSummary? person = await people.FindAsync(account.PersonId, cancellationToken);
        string studentName = string.IsNullOrWhiteSpace(person?.DisplayName)
            ? "Student"
            : person.DisplayName.Trim();
        string effectiveClosureDate = (account.ClosedAtUtc ?? DateTimeOffset.UtcNow)
            .UtcDateTime
            .ToString("dd MMM yyyy", CultureInfo.InvariantCulture);
        string remainingBalance = account.CachedBalance == 0m
            ? "None"
            : $"SGD {account.CachedBalance:N2}";
        string refundDestination = account.CachedBalance == 0m
            ? "Not required"
            : "Not configured";

        const string subject = "Your Education Account Has Been Closed";
        string plainTextBody = string.Join(Environment.NewLine, [
            "MOE SEEDS",
            "Education Account closure",
            string.Empty,
            $"Hello {studentName},",
            string.Empty,
            $"Your Education Account (Account ID: {account.Id}) was closed on {effectiveClosureDate}, due to: {reason}.",
            string.Empty,
            $"Remaining balance: {remainingBalance}",
            $"Refund destination: {refundDestination}"
        ]);

        string htmlBody = BuildClosedHtmlBody(
            studentName,
            account.Id,
            effectiveClosureDate,
            reason,
            remainingBalance,
            refundDestination);

        await SendAsync(recipient.EmailAddress, subject, plainTextBody, htmlBody, account.Id, "closed", cancellationToken);
    }

    public async Task SendPendingClosureAsync(
        EducationAccount account,
        decimal outstandingAmount,
        DateOnly deadlineDate,
        CancellationToken cancellationToken)
    {
        EmailRecipient? recipient = await ResolveRecipientAsync(account, "pending closure", cancellationToken);
        if (recipient is null)
        {
            return;
        }

        PersonSummary? person = await people.FindAsync(account.PersonId, cancellationToken);
        string studentName = string.IsNullOrWhiteSpace(person?.DisplayName)
            ? "Student"
            : person.DisplayName.Trim();
        string outstandingDisplay = $"SGD {outstandingAmount:N2}";
        string deadlineDisplay = deadlineDate.ToString("dd MMM yyyy", CultureInfo.InvariantCulture);

        const string subject = "Action Required: Outstanding Balance Before Account Closure";
        string plainTextBody = string.Join(Environment.NewLine, [
            "MOE SEEDS",
            "Education Account pending closure",
            string.Empty,
            $"Hello {studentName},",
            string.Empty,
            $"Your Education Account is scheduled for closure. Before this can be finalised, you have an outstanding charge of {outstandingDisplay} that must be settled.",
            string.Empty,
            $"Deadline to settle: {deadlineDisplay}",
            string.Empty,
            "Please log in to the Payment Dashboard to resolve this.",
            $"Go to Payment Dashboard -> {PaymentDashboardUrl}"
        ]);

        string htmlBody = BuildPendingClosureHtmlBody(
            studentName,
            outstandingDisplay,
            deadlineDisplay);

        await SendAsync(recipient.EmailAddress, subject, plainTextBody, htmlBody, account.Id, "pending closure", cancellationToken);
    }

    private async Task SendAsync(
        string recipientEmail,
        string subject,
        string plainTextBody,
        string htmlBody,
        long accountId,
        string notificationType,
        CancellationToken cancellationToken)
    {
        try
        {
            Result result = await mailGateway.SendAsync(
                new EmailDeliveryMessage(recipientEmail, subject, plainTextBody, htmlBody),
                cancellationToken);

            if (result.IsFailure)
            {
                logger.LogWarning(
                    "Education Account {NotificationType} email notification failed. EducationAccountId={EducationAccountId} ErrorCode={ErrorCode}",
                    notificationType,
                    accountId,
                    result.Error.Code);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(
                ex,
                "Education Account {NotificationType} email notification threw an exception. EducationAccountId={EducationAccountId}",
                notificationType,
                accountId);
        }
    }

    private async Task<EmailRecipient?> ResolveRecipientAsync(
        EducationAccount account,
        string notificationType,
        CancellationToken cancellationToken)
    {
        try
        {
            EmailRecipient? recipient = await recipientResolver.ResolveForPersonAsync(account.PersonId, cancellationToken);
            if (recipient is null)
            {
                logger.LogWarning(
                    "Education Account {NotificationType} email skipped because no valid recipient was found. PersonId={PersonId} EducationAccountId={EducationAccountId}",
                    notificationType,
                    account.PersonId,
                    account.Id);
            }

            return recipient;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(
                ex,
                "Education Account {NotificationType} email recipient resolution failed. PersonId={PersonId} EducationAccountId={EducationAccountId}",
                notificationType,
                account.PersonId,
                account.Id);
            return null;
        }
    }

    private static string BuildClosedHtmlBody(
        string studentName,
        long accountId,
        string effectiveClosureDate,
        string reason,
        string remainingBalance,
        string refundDestination)
    {
        StringBuilder builder = StartBody("Education Account closed");
        builder.Append("<p style=\"font-size:16px;line-height:24px;margin:0 0 18px;color:#172033;\">Hello ")
            .Append(WebUtility.HtmlEncode(studentName))
            .Append(",</p>");
        builder.Append("<p style=\"font-size:15px;line-height:23px;margin:0 0 24px;color:#46566d;\">Your Education Account (Account ID: <strong>")
            .Append(accountId)
            .Append("</strong>) was closed on <strong>")
            .Append(WebUtility.HtmlEncode(effectiveClosureDate))
            .Append("</strong>, due to: ")
            .Append(WebUtility.HtmlEncode(reason))
            .Append(".</p>");
        AppendSummaryTable(builder, [
            ("Remaining balance", remainingBalance),
            ("Refund destination", refundDestination)
        ]);
        return EndBody(builder);
    }

    private static string BuildPendingClosureHtmlBody(
        string studentName,
        string outstandingAmount,
        string deadlineDate)
    {
        StringBuilder builder = StartBody("Action required before closure");
        builder.Append("<p style=\"font-size:16px;line-height:24px;margin:0 0 18px;color:#172033;\">Hello ")
            .Append(WebUtility.HtmlEncode(studentName))
            .Append(",</p>");
        builder.Append("<p style=\"font-size:15px;line-height:23px;margin:0 0 24px;color:#46566d;\">Your Education Account is scheduled for closure. Before this can be finalised, please settle the outstanding charge below.</p>");
        AppendSummaryTable(builder, [
            ("Outstanding amount", outstandingAmount),
            ("Deadline to settle", deadlineDate)
        ]);
        builder.Append("<p style=\"font-size:15px;line-height:23px;margin:0 0 24px;color:#46566d;\">Please log in to the Payment Dashboard to resolve this.</p>");
        EmailTemplateBranding.AppendButton(builder, PaymentDashboardUrl, "Go to Payment Dashboard");
        return EndBody(builder);
    }

    private static StringBuilder StartBody(string title)
    {
        StringBuilder builder = new();
        builder.Append("<!doctype html><html><head><meta http-equiv=\"Content-Type\" content=\"text/html; charset=utf-8\"></head>");
        builder.Append("<body bgcolor=\"#eef4fb\" style=\"margin:0;padding:0;background-color:#eef4fb;font-family:Arial,Helvetica,sans-serif;color:#172033;\">");
        builder.Append("<table role=\"presentation\" width=\"100%\" border=\"0\" cellspacing=\"0\" cellpadding=\"0\" bgcolor=\"#eef4fb\" style=\"background-color:#eef4fb;\">");
        builder.Append("<tr><td align=\"center\" style=\"padding:28px 12px;\">");
        builder.Append("<table role=\"presentation\" width=\"640\" border=\"0\" cellspacing=\"0\" cellpadding=\"0\" bgcolor=\"#ffffff\" style=\"width:640px;max-width:100%;background-color:#ffffff;border:1px solid #dce3ee;\">");
        EmailTemplateBranding.AppendHeader(builder, title);
        builder.Append("<tr><td style=\"padding:30px;\">");
        return builder;
    }

    private static string EndBody(StringBuilder builder)
    {
        builder.Append("</td></tr>");
        builder.Append("<tr><td bgcolor=\"#f8fafc\" style=\"background-color:#f8fafc;padding:18px 30px;color:#64748b;font-size:12px;line-height:18px;\">This message was sent by MOE SEEDS.</td></tr>");
        builder.Append("</table></td></tr></table></body></html>");
        return builder.ToString();
    }

    private static void AppendSummaryTable(
        StringBuilder builder,
        IReadOnlyCollection<(string Label, string Value)> rows)
    {
        builder.Append("<table role=\"presentation\" width=\"100%\" border=\"0\" cellspacing=\"0\" cellpadding=\"0\" style=\"border-collapse:collapse;margin:0 0 24px;\">");
        foreach ((string label, string value) in rows)
        {
            builder.Append("<tr><td bgcolor=\"")
                .Append(EmailTemplateBranding.PrimarySoftColor)
                .Append("\" style=\"background-color:")
                .Append(EmailTemplateBranding.PrimarySoftColor)
                .Append(";padding:14px 16px;border-bottom:8px solid #ffffff;\">");
            builder.Append("<div style=\"font-size:12px;line-height:18px;color:#64748b;text-transform:uppercase;font-weight:bold;letter-spacing:1px;\">")
                .Append(WebUtility.HtmlEncode(label))
                .Append("</div>");
            builder.Append("<div style=\"font-size:20px;line-height:28px;color:")
                .Append(EmailTemplateBranding.PrimaryTextColor)
                .Append(";font-weight:bold;padding-top:4px;\">")
                .Append(WebUtility.HtmlEncode(value))
                .Append("</div></td></tr>");
        }

        builder.Append("</table>");
    }
}
