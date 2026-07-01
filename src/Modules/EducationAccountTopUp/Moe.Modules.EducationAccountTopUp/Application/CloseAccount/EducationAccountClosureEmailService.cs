using System.Globalization;
using System.Net;
using System.Text;
using Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;
using Moe.Modules.IdentityPlatform.IGateway.People;
using Moe.Modules.MailDelivery.IGateway;
using Moe.Modules.MailDelivery.Templates;

namespace Moe.Modules.EducationAccountTopUp.Application.CloseAccount;

internal sealed class EducationAccountClosureEmailService(
    IPersonDirectory people,
    IEmailNotificationScheduler mailScheduler,
    IEmailBrandingProvider branding)
{
    public async Task SendClosedAsync(
        EducationAccount account,
        string reason,
        CancellationToken cancellationToken)
    {
        PersonSummary? person = await people.FindAsync(account.PersonId, cancellationToken);
        string studentName = string.IsNullOrWhiteSpace(person?.DisplayName)
            ? "Student"
            : person.DisplayName.Trim();
        string effectiveClosureDate = (account.ClosedAtUtc ?? DateTimeOffset.UtcNow)
            .UtcDateTime
            .ToString("dd MMM yyyy", CultureInfo.InvariantCulture);
        string remainingBalance = account.CachedBalance == 0m
            ? "None"
            : EmailTemplateBranding.FormatMoney(account.CachedBalance);
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

        await EnqueueAsync(account, subject, plainTextBody, htmlBody, "NOTI-06-CLOSED", cancellationToken);
    }

    public async Task SendPendingClosureAsync(
        EducationAccount account,
        decimal outstandingAmount,
        DateOnly deadlineDate,
        CancellationToken cancellationToken)
    {
        PersonSummary? person = await people.FindAsync(account.PersonId, cancellationToken);
        string studentName = string.IsNullOrWhiteSpace(person?.DisplayName)
            ? "Student"
            : person.DisplayName.Trim();
        string outstandingDisplay = EmailTemplateBranding.FormatMoney(outstandingAmount);
        string deadlineDisplay = EmailTemplateBranding.FormatDate(deadlineDate);

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

        await EnqueueAsync(account, subject, plainTextBody, htmlBody, "NOTI-06-PENDING", cancellationToken);
    }

    private async Task EnqueueAsync(
        EducationAccount account,
        string subject,
        string plainTextBody,
        string htmlBody,
        string notificationCode,
        CancellationToken cancellationToken)
    {
        await mailScheduler.EnqueueForPersonAsync(
            notificationCode,
            account.PersonId,
            subject,
            plainTextBody,
            htmlBody,
            "EducationAccount",
            account.Id.ToString(CultureInfo.InvariantCulture),
            cancellationToken);
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
            EmailTemplateBranding.AppendSummaryRow(
                builder,
                label,
                value,
                EmailTemplateBranding.PrimarySoftColor,
                EmailTemplateBranding.PrimaryTextColor);
        }

        builder.Append("</table>");
    }
}
