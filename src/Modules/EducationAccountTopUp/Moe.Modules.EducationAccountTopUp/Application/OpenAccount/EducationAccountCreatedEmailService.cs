using System.Globalization;
using System.Net;
using System.Text;
using Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;
using Moe.Modules.IdentityPlatform.IGateway.People;
using Moe.Modules.MailDelivery.IGateway;
using Moe.Modules.MailDelivery.Templates;

namespace Moe.Modules.EducationAccountTopUp.Application.OpenAccount;

internal sealed class EducationAccountCreatedEmailService(
    IPersonDirectory people,
    IEmailNotificationScheduler mailScheduler,
    IEmailBrandingProvider branding)
{
    public async Task SendAsync(EducationAccount account, CancellationToken cancellationToken)
    {
        PersonSummary? person = await people.FindAsync(account.PersonId, cancellationToken);
        string studentName = string.IsNullOrWhiteSpace(person?.DisplayName)
            ? "Student"
            : person.DisplayName.Trim();
        string activationDate = account.OpenedAtUtc.UtcDateTime
            .ToString("dd MMM yyyy", CultureInfo.InvariantCulture);

        const string subject = "MOE - Your Education Account has been created!";
        string plainTextBody = string.Join(Environment.NewLine, [
            branding.AppName,
            "Education Account created",
            string.Empty,
            $"Hello {studentName},",
            string.Empty,
            "Your Education Account with the Singapore Ministry of Education has been created.",
            string.Empty,
            $"Account ID: {account.Id}",
            $"Date of Activation: {activationDate}",
            string.Empty,
            "Please log in to the e-Service portal to view your account details and explore available courses.",
            $"Login to e-Service Portal -> {branding.AccountPortalUrl}"
        ]);

        string htmlBody = BuildHtmlBody(studentName, account.Id, activationDate, branding.AppName, branding.AccountPortalUrl);

        await mailScheduler.EnqueueForPersonAsync(
            "NOTI-07",
            account.PersonId,
            subject,
            plainTextBody,
            htmlBody,
            "EducationAccount",
            account.Id.ToString(CultureInfo.InvariantCulture),
            cancellationToken);
    }

    private static string BuildHtmlBody(string studentName, long accountId, string activationDate, string appName, string accountPortalUrl)
    {
        StringBuilder builder = new();
        EmailTemplateBranding.AppendShellStart(builder);
        EmailTemplateBranding.AppendHeader(builder, "Education Account created", appName);
        builder.Append("<tr><td style=\"padding:30px;\">");
        builder.Append("<p style=\"font-size:16px;line-height:24px;margin:0 0 18px;color:#172033;\">Hello ")
            .Append(WebUtility.HtmlEncode(studentName))
            .Append(",</p>");
        builder.Append("<p style=\"font-size:15px;line-height:23px;margin:0 0 24px;color:#46566d;\">Your Education Account with the Singapore Ministry of Education has been created and is ready to use.</p>");
        builder.Append("<table role=\"presentation\" width=\"100%\" border=\"0\" cellspacing=\"0\" cellpadding=\"0\" style=\"border-collapse:collapse;margin:0 0 24px;\">");
        EmailTemplateBranding.AppendSummaryRow(builder, "Account ID", accountId.ToString(CultureInfo.InvariantCulture), EmailTemplateBranding.PrimarySoftColor, EmailTemplateBranding.PrimaryTextColor);
        EmailTemplateBranding.AppendSummaryRow(builder, "Date of Activation", activationDate, EmailTemplateBranding.PrimarySoftColor, EmailTemplateBranding.PrimaryTextColor);
        builder.Append("</table>");
        builder.Append("<p style=\"font-size:15px;line-height:23px;margin:0 0 24px;color:#46566d;\">Please log in to the e-Service portal to view your account details and explore available courses.</p>");
        EmailTemplateBranding.AppendButton(builder, accountPortalUrl, "Login to e-Service Portal");
        builder.Append("</td></tr>");
        EmailTemplateBranding.AppendFooter(builder, $"This message was sent by {appName}.");
        return builder.ToString();
    }
}
