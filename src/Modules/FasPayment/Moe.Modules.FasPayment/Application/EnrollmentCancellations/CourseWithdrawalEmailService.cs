using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moe.Modules.IdentityPlatform.Domain.People;
using Moe.Modules.IdentityPlatform.IGateway.People;
using Moe.Modules.MailDelivery.IGateway;
using Moe.Modules.MailDelivery.Templates;
using Moe.Modules.FasPayment.IGateway.Payments;
using Moe.SharedKernel.Results;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.FasPayment.Application.EnrollmentCancellations;

internal sealed class CourseWithdrawalEmailService(
    MoeDbContext dbContext,
    IEmailRecipientResolver recipientResolver,
    IEmailDeliveryGateway mailGateway,
    IEmailDeliverySwitch mailSwitch,
    ILogger<CourseWithdrawalEmailService> logger)
{
    private const string PaymentsUrl = "http://localhost:5173/portal/payments";

    public async Task SendAsync(
        EnrollmentCancellationSnapshot snapshot,
        EnrollmentRefundCalculation calculation,
        EnrollmentRefundExecutionResult? refundResult,
        CancellationToken cancellationToken)
    {
        if (!mailSwitch.IsEnabled)
        {
            logger.LogInformation(
                "Course withdrawal email skipped because MailDelivery is disabled. PersonId={PersonId} CourseEnrollmentId={CourseEnrollmentId}",
                snapshot.Enrollment.PersonId,
                snapshot.Enrollment.Id);
            return;
        }

        EmailRecipient? recipient;
        try
        {
            recipient = await recipientResolver.ResolveForPersonAsync(snapshot.Enrollment.PersonId, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Course withdrawal email recipient resolution failed. PersonId={PersonId} CourseEnrollmentId={CourseEnrollmentId}", snapshot.Enrollment.PersonId, snapshot.Enrollment.Id);
            return;
        }

        if (recipient is null)
        {
            logger.LogWarning("Course withdrawal email skipped because no valid recipient was found. PersonId={PersonId} CourseEnrollmentId={CourseEnrollmentId}", snapshot.Enrollment.PersonId, snapshot.Enrollment.Id);
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
            "MOE SEEDS",
            "Course withdrawal confirmed",
            string.Empty,
            $"Hello {studentName}, your withdrawal from {courseName} has been processed.",
            refundInfo,
            string.Empty,
            $"View Payments -> {PaymentsUrl}"
        ]);
        string htmlBody = BuildHtmlBody(studentName, courseName, refundInfo);

        try
        {
            Result result = await mailGateway.SendAsync(
                new EmailDeliveryMessage(recipient.EmailAddress, subject, plainTextBody, htmlBody),
                cancellationToken);

            if (result.IsFailure)
            {
                logger.LogWarning(
                    "Course withdrawal email failed. CourseEnrollmentId={CourseEnrollmentId} ErrorCode={ErrorCode}",
                    snapshot.Enrollment.Id,
                    result.Error.Code);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Course withdrawal email threw an exception. CourseEnrollmentId={CourseEnrollmentId}", snapshot.Enrollment.Id);
        }
    }

    private static string BuildRefundInfo(
        EnrollmentRefundCalculation calculation,
        EnrollmentRefundExecutionResult? refundResult)
    {
        if (calculation.RefundAmount <= 0m)
        {
            return "No refund is due for this withdrawal.";
        }

        string amount = $"SGD {calculation.RefundAmount:N2}";
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
        string refundInfo)
    {
        StringBuilder builder = new();
        EmailTemplateBranding.AppendShellStart(builder);
        EmailTemplateBranding.AppendHeader(builder, "Course withdrawal confirmed");
        builder.Append("<tr><td style=\"padding:30px;\">");
        builder.Append("<p style=\"font-size:16px;line-height:24px;margin:0 0 18px;color:#172033;\">Hello ")
            .Append(WebUtility.HtmlEncode(studentName))
            .Append(", your withdrawal has been processed.</p>");
        builder.Append("<table role=\"presentation\" width=\"100%\" border=\"0\" cellspacing=\"0\" cellpadding=\"0\" style=\"border-collapse:collapse;margin:0 0 24px;\">");
        AppendSummaryRow(builder, "Course", courseName, EmailTemplateBranding.PrimarySoftColor, EmailTemplateBranding.PrimaryTextColor);
        AppendSummaryRow(builder, "Refund", refundInfo, "#f8fafc", "#334155");
        builder.Append("</table>");
        EmailTemplateBranding.AppendButton(builder, PaymentsUrl, "View Payments");
        builder.Append("</td></tr>");
        builder.Append("<tr><td bgcolor=\"#f8fafc\" style=\"background-color:#f8fafc;padding:18px 30px;color:#64748b;font-size:12px;line-height:18px;\">This message was sent by MOE SEEDS after your course withdrawal was confirmed.</td></tr>");
        builder.Append("</table></td></tr></table></body></html>");
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
        builder.Append("<div style=\"font-size:20px;line-height:28px;color:")
            .Append(valueColor)
            .Append(";font-weight:bold;padding-top:4px;\">")
            .Append(WebUtility.HtmlEncode(value))
            .Append("</div></td></tr>");
    }
}
