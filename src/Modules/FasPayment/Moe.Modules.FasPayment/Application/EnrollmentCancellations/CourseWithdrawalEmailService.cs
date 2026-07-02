using System.Globalization;
using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Moe.Modules.IdentityPlatform.Domain.People;
using Moe.Modules.MailDelivery.IGateway;
using Moe.Modules.MailDelivery.Templates;
using Moe.Modules.FasPayment.IGateway.Payments;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.FasPayment.Application.EnrollmentCancellations;

internal sealed class CourseWithdrawalEmailService(
    MoeDbContext dbContext,
    IEmailNotificationScheduler mailScheduler,
    IEmailBrandingProvider branding)
{
    public async Task SendAsync(
        EnrollmentCancellationSnapshot snapshot,
        EnrollmentRefundCalculation calculation,
        EnrollmentRefundExecutionResult? refundResult,
        CancellationToken cancellationToken)
    {
        if (!mailScheduler.IsEnabled)
        {
            return;
        }

        Person? person = await dbContext.Set<Person>()
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == snapshot.Enrollment.PersonId, cancellationToken);

        string studentName = string.IsNullOrWhiteSpace(person?.OfficialFullName)
            ? "Student"
            : person.OfficialFullName.Trim();
        string courseName = string.IsNullOrWhiteSpace(snapshot.Course.CourseName)
            ? snapshot.Course.CourseCode
            : snapshot.Course.CourseName.Trim();
        string refundInfo = BuildRefundInfo(calculation, refundResult);
        string subject = $"Your Withdrawal from {courseName} Is Confirmed";
        string plainTextBody = string.Join(Environment.NewLine, [
            branding.AppName,
            "Course withdrawal confirmed",
            string.Empty,
            $"Hello {studentName}, your withdrawal from {courseName} has been processed.",
            refundInfo,
            string.Empty,
            $"View Payments -> {branding.PaymentDashboardUrl}"
        ]);
        string htmlBody = BuildHtmlBody(studentName, courseName, refundInfo, branding.AppName, branding.PaymentDashboardUrl);

        await mailScheduler.EnqueueForPersonAsync(
            "NOTI-10",
            snapshot.Enrollment.PersonId,
            subject,
            plainTextBody,
            htmlBody,
            "CourseEnrollment",
            snapshot.Enrollment.Id.ToString(CultureInfo.InvariantCulture),
            cancellationToken);
    }

    private static string BuildRefundInfo(
        EnrollmentRefundCalculation calculation,
        EnrollmentRefundExecutionResult? refundResult)
    {
        if (calculation.RefundAmount <= 0m)
        {
            return "No refund is due for this withdrawal.";
        }

        string amount = EmailTemplateBranding.FormatMoney(calculation.RefundAmount);
        string destination = (calculation.EducationAccountRefundAmount > 0m, calculation.OnlineRefundAmount > 0m) switch
        {
            (true, true) => "your Education Account and original online payment method",
            (true, false) => "your Education Account",
            (false, true) => "your original online payment method",
            _ => "your payment source"
        };
        string status = string.IsNullOrWhiteSpace(refundResult?.RefundStatusCode)
            ? string.Empty
            : $" Status: {refundResult.RefundStatusCode}.";
        return $"Refund Amount: {amount} to {destination}.{status}";
    }

    private static string BuildHtmlBody(
        string studentName,
        string courseName,
        string refundInfo,
        string appName,
        string paymentDashboardUrl)
    {
        StringBuilder builder = new();
        EmailTemplateBranding.AppendShellStart(builder);
        EmailTemplateBranding.AppendHeader(builder, "Course withdrawal confirmed", appName);
        builder.Append("<tr><td style=\"padding:30px;\">");
        builder.Append("<p style=\"font-size:16px;line-height:24px;margin:0 0 18px;color:#172033;\">Hello ")
            .Append(WebUtility.HtmlEncode(studentName))
            .Append(", your withdrawal has been processed.</p>");
        builder.Append("<table role=\"presentation\" width=\"100%\" border=\"0\" cellspacing=\"0\" cellpadding=\"0\" style=\"border-collapse:collapse;margin:0 0 24px;\">");
        EmailTemplateBranding.AppendSummaryRow(builder, "Course", courseName, EmailTemplateBranding.PrimarySoftColor, EmailTemplateBranding.PrimaryTextColor);
        EmailTemplateBranding.AppendSummaryRow(builder, "Refund", refundInfo);
        builder.Append("</table>");
        EmailTemplateBranding.AppendButton(builder, paymentDashboardUrl, "View Payments");
        builder.Append("</td></tr>");
        EmailTemplateBranding.AppendFooter(builder, $"This message was sent by {appName} after your course withdrawal was confirmed.");
        return builder.ToString();
    }
}
