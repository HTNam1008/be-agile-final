using System.Globalization;
using System.Net;
using System.Text;
using Moe.Modules.MailDelivery.IGateway;
using Moe.Modules.MailDelivery.Templates;

namespace Moe.Modules.IdentityPlatform.Application.Students;

internal sealed class StudentAccountNotificationEmailService(
    IEmailNotificationScheduler mailScheduler,
    IEmailBrandingProvider branding)
{
    public const string AccountCreatedNotificationType = "NOTI-15-ACCOUNT-CREATED";
    public const string AccountDisabledNotificationType = "NOTI-16-ACCOUNT-DISABLED";
    public const string AccountEnabledNotificationType = "NOTI-17-ACCOUNT-ENABLED";

    public Task SendStudentAccountCreatedAsync(
        long personId,
        string? displayName,
        string? schoolName,
        DateTime createdAtUtc,
        CancellationToken cancellationToken)
    {
        string studentName = NormalizeDisplayName(displayName);
        string createdDate = EmailTemplateBranding.FormatDate(createdAtUtc);
        const string title = "Portal access ready";
        const string subject = "Your Student Portal Account Is Ready";
        string schoolLine = string.IsNullOrWhiteSpace(schoolName)
            ? "School: Not specified"
            : $"School: {schoolName.Trim()}";

        string plainTextBody = string.Join(Environment.NewLine, [
            branding.AppName,
            title,
            string.Empty,
            $"Hello {studentName},",
            string.Empty,
            "Your student profile has been created. You can access the e-Service portal with Singpass when your access is available and you are eligible.",
            string.Empty,
            schoolLine,
            $"Created: {createdDate}",
            string.Empty,
            $"Go to e-Service Portal -> {branding.AccountPortalUrl}"
        ]);

        string htmlBody = BuildHtmlBody(
            title,
            studentName,
            "Your student profile has been created. You can access the e-Service portal with Singpass when your access is available and you are eligible.",
            [
                ("School", string.IsNullOrWhiteSpace(schoolName) ? "Not specified" : schoolName.Trim()),
                ("Created", createdDate)
            ],
            "Go to e-Service Portal",
            branding.AccountPortalUrl);

        return EnqueueAsync(
            AccountCreatedNotificationType,
            personId,
            subject,
            plainTextBody,
            htmlBody,
            personId.ToString(CultureInfo.InvariantCulture),
            cancellationToken);
    }

    public Task SendStudentAccountDisabledAsync(
        long personId,
        string? displayName,
        DateTime disabledAtUtc,
        CancellationToken cancellationToken)
    {
        string disabledDate = EmailTemplateBranding.FormatDate(disabledAtUtc);
        const string title = "Student account disabled";
        const string subject = "Your Student Portal Account Has Been Disabled";
        string studentName = NormalizeDisplayName(displayName);
        string plainTextBody = string.Join(Environment.NewLine, [
            branding.AppName,
            title,
            string.Empty,
            $"Hello {studentName},",
            string.Empty,
            "Your student portal account has been disabled. You will not be able to access the e-Service portal while the account is disabled.",
            string.Empty,
            $"Disabled: {disabledDate}",
            string.Empty,
            "If you believe this is incorrect, please contact your school administrator."
        ]);

        string htmlBody = BuildHtmlBody(
            title,
            studentName,
            "Your student portal account has been disabled. You will not be able to access the e-Service portal while the account is disabled.",
            [("Disabled", disabledDate)],
            null,
            null);

        return EnqueueAsync(
            AccountDisabledNotificationType,
            personId,
            subject,
            plainTextBody,
            htmlBody,
            personId.ToString(CultureInfo.InvariantCulture),
            cancellationToken);
    }

    public Task SendStudentAccountEnabledAsync(
        long personId,
        string? displayName,
        DateTime enabledAtUtc,
        CancellationToken cancellationToken)
    {
        string enabledDate = EmailTemplateBranding.FormatDate(enabledAtUtc);
        const string title = "Student account enabled";
        const string subject = "Your Student Portal Account Has Been Enabled";
        string studentName = NormalizeDisplayName(displayName);
        string plainTextBody = string.Join(Environment.NewLine, [
            branding.AppName,
            title,
            string.Empty,
            $"Hello {studentName},",
            string.Empty,
            "Your student portal account has been enabled. You can access the e-Service portal with Singpass when your access is available and you are eligible.",
            string.Empty,
            $"Enabled: {enabledDate}",
            string.Empty,
            $"Go to e-Service Portal -> {branding.AccountPortalUrl}"
        ]);

        string htmlBody = BuildHtmlBody(
            title,
            studentName,
            "Your student portal account has been enabled. You can access the e-Service portal with Singpass when your access is available and you are eligible.",
            [("Enabled", enabledDate)],
            "Go to e-Service Portal",
            branding.AccountPortalUrl);

        return EnqueueAsync(
            AccountEnabledNotificationType,
            personId,
            subject,
            plainTextBody,
            htmlBody,
            personId.ToString(CultureInfo.InvariantCulture),
            cancellationToken);
    }

    private Task<bool> EnqueueAsync(
        string notificationType,
        long personId,
        string subject,
        string plainTextBody,
        string htmlBody,
        string entityId,
        CancellationToken cancellationToken)
        => mailScheduler.EnqueueForPersonAsync(
            notificationType,
            personId,
            subject,
            plainTextBody,
            htmlBody,
            "StudentAccount",
            entityId,
            cancellationToken);

    private string BuildHtmlBody(
        string title,
        string studentName,
        string leadText,
        IReadOnlyCollection<(string Label, string Value)> summaryRows,
        string? buttonLabel,
        string? buttonUrl)
    {
        StringBuilder builder = new();
        EmailTemplateBranding.AppendShellStart(builder);
        EmailTemplateBranding.AppendHeader(builder, title, branding.AppName);
        builder.Append("<tr><td style=\"padding:30px;\">");
        builder.Append("<p style=\"font-size:16px;line-height:24px;margin:0 0 18px;color:#172033;\">Hello ")
            .Append(WebUtility.HtmlEncode(studentName))
            .Append(",</p>");
        builder.Append("<p style=\"font-size:15px;line-height:23px;margin:0 0 24px;color:#46566d;\">")
            .Append(WebUtility.HtmlEncode(leadText))
            .Append("</p>");

        if (summaryRows.Count > 0)
        {
            builder.Append("<table role=\"presentation\" width=\"100%\" border=\"0\" cellspacing=\"0\" cellpadding=\"0\" style=\"border-collapse:collapse;margin:0 0 24px;\">");
            foreach ((string label, string value) in summaryRows)
            {
                EmailTemplateBranding.AppendSummaryRow(builder, label, value);
            }

            builder.Append("</table>");
        }

        if (!string.IsNullOrWhiteSpace(buttonLabel) && !string.IsNullOrWhiteSpace(buttonUrl))
        {
            EmailTemplateBranding.AppendButton(builder, buttonUrl, buttonLabel);
        }

        builder.Append("</td></tr>");
        EmailTemplateBranding.AppendFooter(builder, $"This message was sent by {branding.AppName}.");
        return builder.ToString();
    }

    private static string NormalizeDisplayName(string? displayName)
        => string.IsNullOrWhiteSpace(displayName) ? "Student" : displayName.Trim();
}
