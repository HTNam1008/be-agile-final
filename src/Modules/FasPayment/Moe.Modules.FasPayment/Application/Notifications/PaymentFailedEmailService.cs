using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moe.Modules.FasPayment.Domain.Payments;
using Moe.Modules.IdentityPlatform.Domain.People;
using Moe.Modules.IdentityPlatform.IGateway.People;
using Moe.Modules.MailDelivery.IGateway;
using Moe.Modules.MailDelivery.Templates;
using Moe.SharedKernel.Results;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.FasPayment.Application.Notifications;

internal sealed class PaymentFailedEmailService(
    MoeDbContext dbContext,
    IEmailRecipientResolver recipientResolver,
    IEmailDeliveryGateway mailGateway,
    IEmailDeliverySwitch mailSwitch,
    ILogger<PaymentFailedEmailService> logger)
{
    private const string PaymentDashboardUrl = "http://localhost:5173/portal/payments";

    public async Task SendStatementPaymentFailedAsync(
        Payment payment,
        string failureReason,
        CancellationToken cancellationToken)
    {
        if (!mailSwitch.IsEnabled)
        {
            logger.LogInformation(
                "Statement payment failure email skipped because MailDelivery is disabled. PersonId={PersonId} PaymentId={PaymentId}",
                payment.PayerPersonId,
                payment.Id);
            return;
        }

        EmailRecipient? recipient;
        try
        {
            recipient = await recipientResolver.ResolveForPersonAsync(payment.PayerPersonId, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Statement payment failure email recipient resolution failed. PersonId={PersonId} PaymentId={PaymentId}", payment.PayerPersonId, payment.Id);
            return;
        }

        if (recipient is null)
        {
            logger.LogWarning("Statement payment failure email skipped because no valid recipient was found. PersonId={PersonId} PaymentId={PaymentId}", payment.PayerPersonId, payment.Id);
            return;
        }

        Person? person = await dbContext.Set<Person>()
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == payment.PayerPersonId, cancellationToken);

        string studentName = string.IsNullOrWhiteSpace(person?.OfficialFullName)
            ? "Student"
            : person.OfficialFullName.Trim();
        string amountDisplay = $"SGD {payment.PaymentAmount:N2}";
        string itemName = payment.BillingStatementId is long statementId
            ? $"Monthly billing statement {statementId}"
            : "your bill";
        string reason = string.IsNullOrWhiteSpace(failureReason)
            ? "Payment could not be processed."
            : failureReason.Trim();

        const string subject = "Action Required: Your Payment Failed";
        string plainTextBody = string.Join(Environment.NewLine, [
            "MOE SEEDS",
            "Payment failed",
            string.Empty,
            $"Hello {studentName}, your payment of {amountDisplay} for {itemName} could not be processed.",
            $"Reason: {reason}.",
            string.Empty,
            $"Retry Payment -> {PaymentDashboardUrl}"
        ]);
        string htmlBody = BuildHtmlBody(studentName, amountDisplay, itemName, reason);

        try
        {
            Result result = await mailGateway.SendAsync(
                new EmailDeliveryMessage(recipient.EmailAddress, subject, plainTextBody, htmlBody),
                cancellationToken);

            if (result.IsFailure)
            {
                logger.LogWarning(
                    "Statement payment failure email failed. PaymentId={PaymentId} ErrorCode={ErrorCode}",
                    payment.Id,
                    result.Error.Code);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Statement payment failure email threw an exception. PaymentId={PaymentId}", payment.Id);
        }
    }

    private static string BuildHtmlBody(
        string studentName,
        string amountDisplay,
        string itemName,
        string failureReason)
    {
        StringBuilder builder = new();
        EmailTemplateBranding.AppendShellStart(builder);
        EmailTemplateBranding.AppendHeader(builder, "Action required: payment failed");
        builder.Append("<tr><td style=\"padding:30px;\">");
        builder.Append("<p style=\"font-size:16px;line-height:24px;margin:0 0 18px;color:#172033;\">Hello ")
            .Append(WebUtility.HtmlEncode(studentName))
            .Append(", your payment could not be processed.</p>");
        builder.Append("<table role=\"presentation\" width=\"100%\" border=\"0\" cellspacing=\"0\" cellpadding=\"0\" style=\"border-collapse:collapse;margin:0 0 24px;\">");
        AppendSummaryRow(builder, "Amount", amountDisplay, EmailTemplateBranding.PrimarySoftColor, EmailTemplateBranding.PrimaryTextColor);
        AppendSummaryRow(builder, "Course/Bill", itemName, "#f8fafc", "#334155");
        AppendSummaryRow(builder, "Reason", failureReason, "#fff7ed", "#9a3412");
        builder.Append("</table>");
        EmailTemplateBranding.AppendButton(builder, PaymentDashboardUrl, "Retry Payment");
        builder.Append("</td></tr>");
        builder.Append("<tr><td bgcolor=\"#f8fafc\" style=\"background-color:#f8fafc;padding:18px 30px;color:#64748b;font-size:12px;line-height:18px;\">This message was sent by MOE SEEDS after a failed payment attempt.</td></tr>");
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
