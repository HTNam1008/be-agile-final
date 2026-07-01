using System.Globalization;
using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;
using Moe.Modules.IdentityPlatform.Domain.People;
using Moe.Modules.MailDelivery.IGateway;
using Moe.Modules.MailDelivery.Templates;
using Moe.SharedKernel.Results;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.EducationAccountTopUp.Application.Lifecycle;

internal sealed class Age30AccountLockReminderEmailService(
    MoeDbContext dbContext,
    IEmailNotificationQueue mailQueue,
    IEmailDeliverySwitch mailSwitch,
    IAccountLockReminderOutstandingReader outstandingReader,
    IEmailBrandingProvider branding,
    ILogger<Age30AccountLockReminderEmailService> logger) : IAge30AccountLockReminderEmailService
{
    private const string ActivePersonStatusCode = "ACTIVE";

    private static readonly IReadOnlyCollection<ReminderWindow> ReminderWindows =
    [
        new("6 months", static lockDate => lockDate.AddMonths(-6)),
        new("3 months", static lockDate => lockDate.AddMonths(-3)),
        new("1 week", static lockDate => lockDate.AddDays(-7))
    ];

    public async Task SendDueRemindersAsync(DateOnly today, CancellationToken cancellationToken)
    {
        if (!mailSwitch.IsEnabled)
        {
            logger.LogInformation(
                "Age-30 account lock reminder emails skipped because MailDelivery is disabled. Today={Today}",
                today);
            return;
        }

        AccountLockReminderCandidate[] candidates = await (
                from account in dbContext.Set<EducationAccount>().AsNoTracking()
                join person in dbContext.Set<Person>().AsNoTracking()
                    on account.PersonId equals person.Id
                where account.StatusCode == AccountStatuses.Active
                    && person.PersonStatusCode == ActivePersonStatusCode
                select new AccountLockReminderCandidate(
                    account.Id,
                    account.PersonId,
                    person.OfficialFullName,
                    person.DateOfBirth))
            .ToArrayAsync(cancellationToken);

        foreach (AccountLockReminderCandidate candidate in candidates)
        {
            DateOnly lockDate = candidate.DateOfBirth.AddYears(30);
            ReminderWindow? reminderWindow = ReminderWindows
                .FirstOrDefault(window => window.ReminderDate(lockDate) == today);

            if (reminderWindow is null)
            {
                continue;
            }

            await SendReminderAsync(candidate, lockDate, reminderWindow.Label, cancellationToken);
        }
    }

    private async Task SendReminderAsync(
        AccountLockReminderCandidate candidate,
        DateOnly lockDate,
        string reminderWindowLabel,
        CancellationToken cancellationToken)
    {
        decimal? outstandingAmount = await ResolveOutstandingAmountAsync(candidate, cancellationToken);
        string studentName = string.IsNullOrWhiteSpace(candidate.StudentName)
            ? "Student"
            : candidate.StudentName.Trim();
        string lockDateDisplay = lockDate.ToString("dd MMM yyyy", CultureInfo.InvariantCulture);
        string? outstandingAmountDisplay = outstandingAmount is > 0m
            ? $"SGD {outstandingAmount.Value:N2}"
            : null;

        string subject = $"Reminder: Your {branding.AppName} account will be locked soon";
        string plainTextBody = BuildPlainTextBody(
            studentName,
            lockDateDisplay,
            reminderWindowLabel,
            outstandingAmountDisplay,
            branding.AppName,
            branding.PaymentDashboardUrl);
        string htmlBody = BuildHtmlBody(
            studentName,
            lockDateDisplay,
            reminderWindowLabel,
            outstandingAmountDisplay,
            branding.AppName,
            branding.PaymentDashboardUrl);

        try
        {
            Result result = await mailQueue.EnqueueAsync(
                EmailNotificationJob.ForPerson(
                    "AGE-30-LOCK-REMINDER",
                    candidate.PersonId,
                    subject,
                    plainTextBody,
                    htmlBody,
                    "EducationAccount",
                    candidate.EducationAccountId.ToString(CultureInfo.InvariantCulture)),
                cancellationToken);

            if (result.IsFailure)
            {
                logger.LogWarning(
                    "Age-30 account lock reminder email enqueue failed. PersonId={PersonId} EducationAccountId={EducationAccountId} ErrorCode={ErrorCode}",
                    candidate.PersonId,
                    candidate.EducationAccountId,
                    result.Error.Code);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(
                ex,
                "Age-30 account lock reminder email threw an exception. PersonId={PersonId} EducationAccountId={EducationAccountId}",
                candidate.PersonId,
                candidate.EducationAccountId);
        }
    }

    private async Task<decimal?> ResolveOutstandingAmountAsync(
        AccountLockReminderCandidate candidate,
        CancellationToken cancellationToken)
    {
        try
        {
            return await outstandingReader.FindOutstandingAmountAsync(candidate.PersonId, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(
                ex,
                "Age-30 account lock reminder outstanding lookup failed. PersonId={PersonId} EducationAccountId={EducationAccountId}",
                candidate.PersonId,
                candidate.EducationAccountId);
            return null;
        }
    }

    private static string BuildPlainTextBody(
        string studentName,
        string lockDateDisplay,
        string reminderWindowLabel,
        string? outstandingAmountDisplay,
        string appName,
        string paymentDashboardUrl)
    {
        List<string> lines =
        [
            appName,
            "Account lock reminder",
            string.Empty,
            $"Hello {studentName},",
            string.Empty,
            $"This is a {reminderWindowLabel} reminder that your {appName} Education Account and portal account may be locked when you turn 30 on {lockDateDisplay}.",
            string.Empty
        ];

        if (outstandingAmountDisplay is not null)
        {
            lines.Add($"Outstanding Amount: {outstandingAmountDisplay}");
            lines.Add(string.Empty);
        }

        lines.Add("Please review and settle any outstanding charges before your account is locked.");
        lines.Add($"Go to Payment Dashboard -> {paymentDashboardUrl}");
        return string.Join(Environment.NewLine, lines);
    }

    private static string BuildHtmlBody(
        string studentName,
        string lockDateDisplay,
        string reminderWindowLabel,
        string? outstandingAmountDisplay,
        string appName,
        string paymentDashboardUrl)
    {
        StringBuilder builder = new();
        EmailTemplateBranding.AppendShellStart(builder);
        EmailTemplateBranding.AppendHeader(builder, "Your account will be locked soon", appName);
        builder.Append("<tr><td style=\"padding:30px;\">");
        builder.Append("<p style=\"font-size:16px;line-height:24px;margin:0 0 18px;color:#172033;\">Hello ")
            .Append(WebUtility.HtmlEncode(studentName))
            .Append(",</p>");
        builder.Append("<p style=\"font-size:15px;line-height:23px;margin:0 0 24px;color:#46566d;\">This is a <strong>")
            .Append(WebUtility.HtmlEncode(reminderWindowLabel))
            .Append("</strong> reminder that your ")
            .Append(WebUtility.HtmlEncode(appName))
            .Append(" Education Account and portal account may be locked when you turn 30.</p>");
        builder.Append("<table role=\"presentation\" width=\"100%\" border=\"0\" cellspacing=\"0\" cellpadding=\"0\" style=\"border-collapse:collapse;margin:0 0 24px;\">");
        AppendSummaryRow(builder, "Account lock date", lockDateDisplay);
        AppendSummaryRow(builder, "Reminder window", reminderWindowLabel);
        if (outstandingAmountDisplay is not null)
        {
            AppendSummaryRow(builder, "Outstanding Amount", outstandingAmountDisplay);
        }
        builder.Append("</table>");
        builder.Append("<p style=\"font-size:15px;line-height:23px;margin:0 0 24px;color:#46566d;\">Please review and settle any outstanding charges before your account is locked.</p>");
        EmailTemplateBranding.AppendButton(builder, paymentDashboardUrl, "Go to Payment Dashboard");
        builder.Append("</td></tr>");
        EmailTemplateBranding.AppendFooter(builder, $"This message was sent by {appName}.");
        return builder.ToString();
    }

    private static void AppendSummaryRow(StringBuilder builder, string label, string value)
    {
        builder.Append("<tr><td bgcolor=\"")
            .Append(EmailTemplateBranding.PrimarySoftColor)
            .Append("\" style=\"background-color:")
            .Append(EmailTemplateBranding.PrimarySoftColor)
            .Append(";padding:14px 16px;border-bottom:8px solid #ffffff;\">");
        builder.Append("<div style=\"font-size:12px;line-height:18px;color:#64748b;text-transform:uppercase;font-weight:bold;letter-spacing:1px;\">")
            .Append(WebUtility.HtmlEncode(label))
            .Append("</div><div style=\"font-size:20px;line-height:28px;color:")
            .Append(EmailTemplateBranding.PrimaryTextColor)
            .Append(";font-weight:bold;padding-top:4px;\">")
            .Append(WebUtility.HtmlEncode(value))
            .Append("</div></td></tr>");
    }

    private sealed record ReminderWindow(
        string Label,
        Func<DateOnly, DateOnly> ReminderDate);

    private sealed record AccountLockReminderCandidate(
        long EducationAccountId,
        long PersonId,
        string StudentName,
        DateOnly DateOfBirth);
}
