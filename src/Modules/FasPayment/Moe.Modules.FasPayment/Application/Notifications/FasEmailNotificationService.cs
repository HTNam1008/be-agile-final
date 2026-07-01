using System.Globalization;
using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moe.Modules.FasPayment.Domain.Fas;
using Moe.Modules.MailDelivery.IGateway;
using Moe.Modules.MailDelivery.Templates;
using Moe.SharedKernel.Results;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.FasPayment.Application.Notifications;

public sealed class FasEmailNotificationService(
    MoeDbContext dbContext,
    IEmailNotificationQueue mailQueue,
    IEmailDeliverySwitch mailSwitch,
    IEmailBrandingProvider branding,
    ILogger<FasEmailNotificationService> logger)
{
    public async Task SendSubmissionAcknowledgementAsync(long applicationId, CancellationToken cancellationToken)
    {
        if (IsDisabled("FAS application received", applicationId))
        {
            return;
        }

        FasEmailProjection? projection = await GetApplicationProjectionAsync(applicationId, cancellationToken);
        if (projection is null)
        {
            return;
        }

        await SendAsync(
            subject: "We've Received Your FAS Application",
            title: "FAS application received",
            plainTextBody: string.Join(Environment.NewLine, [
                branding.AppName,
                "FAS application acknowledgement",
                string.Empty,
                $"Hello {projection.StudentName},",
                string.Empty,
                $"Your application for {projection.SchemeName} has been submitted successfully and is now pending review."
            ]),
            htmlBody: BuildHtmlBody(
                "FAS application received",
                projection.StudentName,
                $"Your application for <strong>{WebUtility.HtmlEncode(projection.SchemeName)}</strong> has been submitted successfully and is now pending review.",
                [],
                null,
                null,
                branding.AppName,
                branding.FasPortalUrl),
            applicationId,
            projection.PersonId,
            cancellationToken);
    }

    public async Task SendApplicationApprovedAsync(long applicationId, CancellationToken cancellationToken)
    {
        if (IsDisabled("FAS application approved", applicationId))
        {
            return;
        }

        FasEmailProjection? projection = await GetApplicationProjectionAsync(applicationId, cancellationToken);
        if (projection is null)
        {
            return;
        }

        await SendApprovedAsync(applicationId, projection.PersonId, projection.StudentName, projection.SchemeName, projection.ApprovedAmount, cancellationToken);
    }

    public async Task SendApplicationRejectedAsync(
        long applicationId,
        string rejectionReason,
        CancellationToken cancellationToken)
    {
        if (IsDisabled("FAS application update", applicationId))
        {
            return;
        }

        FasEmailProjection? projection = await GetApplicationProjectionAsync(applicationId, cancellationToken);
        if (projection is null)
        {
            return;
        }

        await SendRejectedAsync(applicationId, projection.PersonId, projection.StudentName, projection.SchemeName, rejectionReason, cancellationToken);
    }

    public async Task SendSchemeApprovedAsync(long applicationSchemeId, CancellationToken cancellationToken)
    {
        if (IsDisabled("FAS scheme approved", applicationSchemeId))
        {
            return;
        }

        FasSchemeEmailProjection? projection = await GetSchemeProjectionAsync(applicationSchemeId, cancellationToken);
        if (projection is null)
        {
            return;
        }

        await SendApprovedAsync(
            projection.ApplicationId,
            projection.PersonId,
            projection.StudentName,
            projection.SchemeName,
            projection.ApprovedAmount,
            cancellationToken);
    }

    public async Task SendSchemeRejectedAsync(
        long applicationSchemeId,
        string rejectionReason,
        CancellationToken cancellationToken)
    {
        if (IsDisabled("FAS scheme rejected", applicationSchemeId))
        {
            return;
        }

        FasSchemeEmailProjection? projection = await GetSchemeProjectionAsync(applicationSchemeId, cancellationToken);
        if (projection is null)
        {
            return;
        }

        await SendRejectedAsync(
            projection.ApplicationId,
            projection.PersonId,
            projection.StudentName,
            projection.SchemeName,
            rejectionReason,
            cancellationToken);
    }

    private async Task SendApprovedAsync(
        long applicationId,
        long personId,
        string studentName,
        string schemeName,
        decimal? approvedAmount,
        CancellationToken cancellationToken)
    {
        string[] summaryRows = approvedAmount.HasValue
            ? [$"Subsidy Amount|SGD {approvedAmount.Value:N2}"]
            : [];

        await SendAsync(
            subject: "Your FAS Application Has Been Approved",
            title: "FAS application approved",
            plainTextBody: string.Join(Environment.NewLine, new string[]
            {
                branding.AppName,
                "FAS application approved",
                string.Empty,
                $"Hello {studentName}, your application for {schemeName} has been approved.",
                string.Empty,
                approvedAmount.HasValue ? $"Subsidy Amount: SGD {approvedAmount.Value:N2}" : string.Empty,
                "Your voucher is now ready to use.",
                $"View My Voucher -> {branding.FasPortalUrl}"
            }.Where(x => x.Length > 0)),
            htmlBody: BuildHtmlBody(
                "FAS application approved",
                studentName,
                $"Your application for <strong>{WebUtility.HtmlEncode(schemeName)}</strong> has been approved.",
                summaryRows,
                "Your voucher is now ready to use.",
                "View My Voucher",
                branding.AppName,
                branding.FasPortalUrl),
            applicationId,
            personId,
            cancellationToken);
    }

    private async Task SendRejectedAsync(
        long applicationId,
        long personId,
        string studentName,
        string schemeName,
        string rejectionReason,
        CancellationToken cancellationToken)
    {
        string reason = string.IsNullOrWhiteSpace(rejectionReason)
            ? "Please review your application details."
            : rejectionReason.Trim();

        await SendAsync(
            subject: "Update on Your FAS Application",
            title: "FAS application update",
            plainTextBody: string.Join(Environment.NewLine, [
                branding.AppName,
                "FAS application update",
                string.Empty,
                $"Hello {studentName}, we're unable to approve your application for {schemeName} at this time.",
                string.Empty,
                $"Reason: {reason}",
                string.Empty,
                "You may review and resubmit your application with the required documents.",
                $"View Application -> {branding.FasPortalUrl}"
            ]),
            htmlBody: BuildHtmlBody(
                "FAS application update",
                studentName,
                $"We're unable to approve your application for <strong>{WebUtility.HtmlEncode(schemeName)}</strong> at this time.",
                [$"Reason|{WebUtility.HtmlEncode(reason)}"],
                "You may review and resubmit your application with the required documents.",
                "View Application",
                branding.AppName,
                branding.FasPortalUrl),
            applicationId,
            personId,
            cancellationToken);
    }

    private bool IsDisabled(string title, long entityId)
    {
        if (mailSwitch.IsEnabled)
        {
            return false;
        }

        logger.LogInformation(
            "FAS email skipped because MailDelivery is disabled. Title={Title} EntityId={EntityId}",
            title,
            entityId);
        return true;
    }

    private async Task<FasEmailProjection?> GetApplicationProjectionAsync(
        long applicationId,
        CancellationToken cancellationToken)
    {
        return await (
            from application in dbContext.Set<FasApplication>().AsNoTracking()
            join scheme in dbContext.Set<FasScheme>().AsNoTracking()
                on application.FasSchemeId equals scheme.Id
            where application.Id == applicationId
            select new FasEmailProjection(
                application.Id,
                application.AccountHolderPersonId,
                string.IsNullOrWhiteSpace(application.StudentName) ? "Student" : application.StudentName,
                scheme.Name,
                (decimal?)null))
            .SingleOrDefaultAsync(cancellationToken);
    }

    private async Task<FasSchemeEmailProjection?> GetSchemeProjectionAsync(
        long applicationSchemeId,
        CancellationToken cancellationToken)
    {
        return await (
            from item in dbContext.Set<FasApplicationScheme>().AsNoTracking()
            join application in dbContext.Set<FasApplication>().AsNoTracking()
                on item.FasApplicationId equals application.Id
            join scheme in dbContext.Set<FasScheme>().AsNoTracking()
                on item.FasSchemeId equals scheme.Id
            where item.Id == applicationSchemeId
            select new FasSchemeEmailProjection(
                application.Id,
                application.AccountHolderPersonId,
                string.IsNullOrWhiteSpace(application.StudentName) ? "Student" : application.StudentName,
                scheme.Name,
                item.ApprovedAmount))
            .SingleOrDefaultAsync(cancellationToken);
    }

    private async Task SendAsync(
        string subject,
        string title,
        string plainTextBody,
        string htmlBody,
        long applicationId,
        long personId,
        CancellationToken cancellationToken)
    {
        try
        {
            Result result = await mailQueue.EnqueueAsync(
                EmailNotificationJob.ForPerson(
                    "NOTI-05",
                    personId,
                    subject,
                    plainTextBody,
                    htmlBody,
                    "FasApplication",
                    applicationId.ToString(CultureInfo.InvariantCulture)),
                cancellationToken);

            if (result.IsFailure)
            {
                logger.LogWarning(
                    "FAS email enqueue failed. Title={Title} ApplicationId={ApplicationId} ErrorCode={ErrorCode}",
                    title,
                    applicationId,
                    result.Error.Code);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(
                ex,
                "FAS email enqueue threw an exception. Title={Title} ApplicationId={ApplicationId}",
                title,
                applicationId);
        }
    }

    private static string BuildHtmlBody(
        string title,
        string studentName,
        string messageHtml,
        IReadOnlyCollection<string> summaryRows,
        string? note,
        string? buttonLabel,
        string appName,
        string fasPortalUrl)
    {
        StringBuilder builder = new();
        EmailTemplateBranding.AppendShellStart(builder);
        EmailTemplateBranding.AppendHeader(builder, title, appName);
        builder.Append("<tr><td style=\"padding:30px;\">");
        builder.Append("<p style=\"font-size:16px;line-height:24px;margin:0 0 18px;color:#172033;\">Hello ")
            .Append(WebUtility.HtmlEncode(studentName))
            .Append(", ")
            .Append(messageHtml)
            .Append("</p>");

        if (summaryRows.Count > 0)
        {
            builder.Append("<table role=\"presentation\" width=\"100%\" border=\"0\" cellspacing=\"0\" cellpadding=\"0\" style=\"border-collapse:collapse;margin:0 0 24px;\">");
            foreach (string row in summaryRows)
            {
                string[] parts = row.Split('|', 2);
                AppendSummaryRow(builder, parts[0], parts.Length > 1 ? parts[1] : string.Empty);
            }
            builder.Append("</table>");
        }

        if (!string.IsNullOrWhiteSpace(note))
        {
            builder.Append("<p style=\"font-size:15px;line-height:23px;margin:0 0 24px;color:#46566d;\">")
                .Append(WebUtility.HtmlEncode(note))
                .Append("</p>");
        }

        if (!string.IsNullOrWhiteSpace(buttonLabel))
        {
            EmailTemplateBranding.AppendButton(builder, fasPortalUrl, buttonLabel);
        }

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
            .Append("</div>");
        builder.Append("<div style=\"font-size:20px;line-height:28px;color:")
            .Append(EmailTemplateBranding.PrimaryTextColor)
            .Append(";font-weight:bold;padding-top:4px;\">")
            .Append(value)
            .Append("</div></td></tr>");
    }

    private sealed record FasEmailProjection(
        long ApplicationId,
        long PersonId,
        string StudentName,
        string SchemeName,
        decimal? ApprovedAmount);

    private sealed record FasSchemeEmailProjection(
        long ApplicationId,
        long PersonId,
        string StudentName,
        string SchemeName,
        decimal? ApprovedAmount);
}
