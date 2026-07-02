using System.Globalization;
using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Moe.Modules.FasPayment.Domain.Fas;
using Moe.Modules.MailDelivery.IGateway;
using Moe.Modules.MailDelivery.Templates;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.FasPayment.Application.Notifications;

public sealed class FasEmailNotificationService(
    MoeDbContext dbContext,
    IEmailNotificationScheduler mailScheduler,
    IEmailBrandingProvider branding)
{
    public async Task SendSubmissionAcknowledgementAsync(long applicationId, CancellationToken cancellationToken)
    {
        FasEmailProjection? projection = await GetApplicationProjectionAsync(applicationId, cancellationToken);
        if (projection is null)
        {
            return;
        }

        await SendAsync(
            subject: "We've Received Your FAS Application",
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
        FasEmailProjection? projection = await GetApplicationProjectionAsync(applicationId, cancellationToken);
        if (projection is null)
        {
            return;
        }

        await SendRejectedAsync(applicationId, projection.PersonId, projection.StudentName, projection.SchemeName, rejectionReason, cancellationToken);
    }

    public async Task SendSchemeApprovedAsync(long applicationSchemeId, CancellationToken cancellationToken)
    {
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
            ? [$"Subsidy Amount|{EmailTemplateBranding.FormatMoney(approvedAmount.Value)}"]
            : [];

        await SendAsync(
            subject: "Your FAS Application Has Been Approved",
            plainTextBody: string.Join(Environment.NewLine, new string[]
            {
                branding.AppName,
                "FAS application approved",
                string.Empty,
                $"Hello {studentName}, your application for {schemeName} has been approved.",
                string.Empty,
                approvedAmount.HasValue ? $"Subsidy Amount: {EmailTemplateBranding.FormatMoney(approvedAmount.Value)}" : string.Empty,
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
                [$"Reason|{reason}"],
                "You may review and resubmit your application with the required documents.",
                "View Application",
                branding.AppName,
                branding.FasPortalUrl),
            applicationId,
            personId,
            cancellationToken);
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
        string plainTextBody,
        string htmlBody,
        long applicationId,
        long personId,
        CancellationToken cancellationToken)
    {
        await mailScheduler.EnqueueForPersonAsync(
            "NOTI-05",
            personId,
            subject,
            plainTextBody,
            htmlBody,
            "FasApplication",
            applicationId.ToString(CultureInfo.InvariantCulture),
            cancellationToken);
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
                EmailTemplateBranding.AppendSummaryRow(builder, parts[0], parts.Length > 1 ? parts[1] : string.Empty, EmailTemplateBranding.PrimarySoftColor, EmailTemplateBranding.PrimaryTextColor);
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
