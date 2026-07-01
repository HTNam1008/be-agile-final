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
    IEmailNotificationQueue mailQueue,
    IEmailDeliverySwitch mailSwitch,
    IEmailBrandingProvider branding,
    ILogger<EducationAccountClosureEmailService> logger)
{
    public async Task SendClosedAsync(
        EducationAccount account,
        string reason,
        CancellationToken cancellationToken)
    {
        if (!mailSwitch.IsEnabled)
        {
            logger.LogInformation(
                "Education Account closed email skipped because MailDelivery is disabled. PersonId={PersonId} EducationAccountId={EducationAccountId}",
                account.PersonId,
                account.Id);
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
            branding.AppName,
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
            refundDestination,
            branding.AppName);

        await EnqueueAsync(account, subject, plainTextBody, htmlBody, "NOTI-06-CLOSED", "closed", cancellationToken);
    }

    public async Task SendPendingClosureAsync(
        EducationAccount account,
        decimal outstandingAmount,
        DateOnly deadlineDate,
        CancellationToken cancellationToken)
    {
        if (!mailSwitch.IsEnabled)
        {
            logger.LogInformation(
                "Education Account pending closure email skipped because MailDelivery is disabled. PersonId={PersonId} EducationAccountId={EducationAccountId}",
                account.PersonId,
                account.Id);
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
            branding.AppName,
            "Education Account pending closure",
            string.Empty,
            $"Hello {studentName},",
            string.Empty,
            $"Your Education Account is scheduled for closure. Before this can be finalised, you have an outstanding charge of {outstandingDisplay} that must be settled.",
            string.Empty,
            $"Deadline to settle: {deadlineDisplay}",
            string.Empty,
            "Please log in to the Payment Dashboard to resolve this.",
            $"Go to Payment Dashboard -> {branding.PaymentDashboardUrl}"
        ]);

        string htmlBody = BuildPendingClosureHtmlBody(
            studentName,
            outstandingDisplay,
            deadlineDisplay,
            branding.AppName,
            branding.PaymentDashboardUrl);

        await EnqueueAsync(account, subject, plainTextBody, htmlBody, "NOTI-06-PENDING", "pending closure", cancellationToken);
    }

    private async Task EnqueueAsync(
        EducationAccount account,
        string subject,
        string plainTextBody,
        string htmlBody,
        string notificationCode,
        string notificationType,
        CancellationToken cancellationToken)
    {
        try
        {
            Result result = await mailQueue.EnqueueAsync(
                EmailNotificationJob.ForPerson(
                    notificationCode,
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
                    "Education Account {NotificationType} email enqueue failed. EducationAccountId={EducationAccountId} ErrorCode={ErrorCode}",
                    notificationType,
                    account.Id,
                    result.Error.Code);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(
                ex,
                "Education Account {NotificationType} email enqueue threw an exception. EducationAccountId={EducationAccountId}",
                notificationType,
                account.Id);
        }
    }

    private static string BuildClosedHtmlBody(
        string studentName,
        long accountId,
        string effectiveClosureDate,
        string reason,
        string remainingBalance,
        string refundDestination,
        string appName)
    {
        StringBuilder builder = StartBody("Education Account closed", appName);
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
        return EndBody(builder, appName);
    }

    private static string BuildPendingClosureHtmlBody(
        string studentName,
        string outstandingAmount,
        string deadlineDate,
        string appName,
        string paymentDashboardUrl)
    {
        StringBuilder builder = StartBody("Action required before closure", appName);
        builder.Append("<p style=\"font-size:16px;line-height:24px;margin:0 0 18px;color:#172033;\">Hello ")
            .Append(WebUtility.HtmlEncode(studentName))
            .Append(",</p>");
        builder.Append("<p style=\"font-size:15px;line-height:23px;margin:0 0 24px;color:#46566d;\">Your Education Account is scheduled for closure. Before this can be finalised, please settle the outstanding charge below.</p>");
        AppendSummaryTable(builder, [
            ("Outstanding amount", outstandingAmount),
            ("Deadline to settle", deadlineDate)
        ]);
        builder.Append("<p style=\"font-size:15px;line-height:23px;margin:0 0 24px;color:#46566d;\">Please log in to the Payment Dashboard to resolve this.</p>");
        EmailTemplateBranding.AppendButton(builder, paymentDashboardUrl, "Go to Payment Dashboard");
        return EndBody(builder, appName);
    }

    private static StringBuilder StartBody(string title, string appName)
    {
        StringBuilder builder = new();
        EmailTemplateBranding.AppendShellStart(builder);
        EmailTemplateBranding.AppendHeader(builder, title, appName);
        builder.Append("<tr><td style=\"padding:30px;\">");
        return builder;
    }

    private static string EndBody(StringBuilder builder, string appName)
    {
        builder.Append("</td></tr>");
        EmailTemplateBranding.AppendFooter(builder, $"This message was sent by {appName}.");
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
