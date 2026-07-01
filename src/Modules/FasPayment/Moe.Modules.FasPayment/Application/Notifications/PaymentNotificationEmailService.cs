using System.Globalization;
using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moe.Modules.FasPayment.Domain.Payments;
using Moe.Modules.IdentityPlatform.Domain.People;
using Moe.Modules.MailDelivery.IGateway;
using Moe.Modules.MailDelivery.Templates;
using Moe.SharedKernel.Results;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.FasPayment.Application.Notifications;

internal sealed class PaymentNotificationEmailService(
    MoeDbContext dbContext,
    IEmailNotificationQueue mailQueue,
    IEmailDeliverySwitch mailSwitch,
    ILogger<PaymentNotificationEmailService> logger)
{
    private const string PaymentDashboardUrl = "http://localhost:5173/portal/payments";
    private const string ExpiredReason = "The payment session expired before completion. Please try again.";

    public async Task SendStatementPaymentFailedAsync(
        Payment payment,
        string failureReason,
        CancellationToken cancellationToken)
        => await SendPaymentStatusAsync(
            payment,
            notificationType: "NOTI-09",
            subject: "Action Required: Your Payment Failed",
            title: "Action required: payment failed",
            leadText: "your payment could not be processed.",
            actionLabel: "Retry Payment",
            footer: "This message was sent by MOE SEEDS after a failed payment attempt.",
            statusLabel: "Reason",
            statusValue: NormalizeReason(failureReason, "Payment could not be processed."),
            cancellationToken);

    public async Task SendPaymentExpiredAsync(
        Payment payment,
        CancellationToken cancellationToken)
        => await SendStatementPaymentFailedAsync(payment, ExpiredReason, cancellationToken);

    public async Task SendPaymentSucceededAsync(
        Payment payment,
        DateTime completedAtUtc,
        CancellationToken cancellationToken)
        => await SendPaymentStatusAsync(
            payment,
            notificationType: "NOTI-12",
            subject: "Payment Received",
            title: "Payment received",
            leadText: "we have received your payment.",
            actionLabel: "View Payments",
            footer: "This message was sent by MOE SEEDS after a completed payment.",
            statusLabel: "Paid On",
            statusValue: FormatDate(completedAtUtc),
            cancellationToken);

    public async Task SendPaymentCancelledAsync(
        Payment payment,
        DateTime cancelledAtUtc,
        bool releasedEducationAccountHold,
        CancellationToken cancellationToken)
    {
        string releaseText = releasedEducationAccountHold
            ? "Any reserved Education Account funds have been released."
            : "No money was charged for this cancelled payment attempt.";

        await SendPaymentStatusAsync(
            payment,
            notificationType: "NOTI-13",
            subject: "Payment Cancelled",
            title: "Payment cancelled",
            leadText: $"your payment attempt was cancelled. {releaseText}",
            actionLabel: "View Payments",
            footer: "This message was sent by MOE SEEDS after a payment attempt was cancelled.",
            statusLabel: "Cancelled On",
            statusValue: FormatDate(cancelledAtUtc),
            cancellationToken);
    }

    private async Task SendPaymentStatusAsync(
        Payment payment,
        string notificationType,
        string subject,
        string title,
        string leadText,
        string actionLabel,
        string footer,
        string statusLabel,
        string statusValue,
        CancellationToken cancellationToken)
    {
        if (!mailSwitch.IsEnabled)
        {
            logger.LogInformation(
                "Payment notification email skipped because MailDelivery is disabled. NotificationType={NotificationType} PersonId={PersonId} PaymentId={PaymentId}",
                notificationType,
                payment.PayerPersonId,
                payment.Id);
            return;
        }

        Person? person = await dbContext.Set<Person>()
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == payment.PayerPersonId, cancellationToken);

        string studentName = string.IsNullOrWhiteSpace(person?.OfficialFullName)
            ? "Student"
            : person.OfficialFullName.Trim();
        string amountDisplay = EmailTemplateBranding.FormatMoney(payment.PaymentAmount);
        string itemName = payment.BillingStatementId is long statementId
            ? $"Monthly billing statement {statementId}"
            : $"Bill {payment.BillId}";
        string reference = string.IsNullOrWhiteSpace(payment.ReceiptNumber)
            ? payment.PaymentNumber
            : payment.ReceiptNumber;

        string plainTextBody = string.Join(Environment.NewLine, [
            "MOE SEEDS",
            subject,
            string.Empty,
            $"Hello {studentName}, {leadText}",
            string.Empty,
            $"Amount: {amountDisplay}",
            $"Course/Bill: {itemName}",
            $"Reference: {reference}",
            $"{statusLabel}: {statusValue}",
            string.Empty,
            $"{actionLabel} -> {PaymentDashboardUrl}",
            string.Empty,
            footer
        ]);
        string htmlBody = BuildHtmlBody(
            title,
            studentName,
            leadText,
            amountDisplay,
            itemName,
            reference,
            statusLabel,
            statusValue,
            actionLabel,
            footer);

        await EnqueueAsync(payment, notificationType, subject, plainTextBody, htmlBody, cancellationToken);
    }

    private async Task EnqueueAsync(
        Payment payment,
        string notificationType,
        string subject,
        string plainTextBody,
        string htmlBody,
        CancellationToken cancellationToken)
    {
        string entityId = payment.Id > 0
            ? payment.Id.ToString(CultureInfo.InvariantCulture)
            : payment.PaymentNumber;

        try
        {
            Result result = await mailQueue.EnqueueAsync(
                EmailNotificationJob.ForPerson(
                    notificationType,
                    payment.PayerPersonId,
                    subject,
                    plainTextBody,
                    htmlBody,
                    "Payment",
                    entityId),
                cancellationToken);

            if (result.IsFailure)
            {
                logger.LogWarning(
                    "Payment notification email enqueue failed. NotificationType={NotificationType} PaymentId={PaymentId} ErrorCode={ErrorCode}",
                    notificationType,
                    payment.Id,
                    result.Error.Code);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(
                ex,
                "Payment notification email threw an exception. NotificationType={NotificationType} PaymentId={PaymentId}",
                notificationType,
                payment.Id);
        }
    }

    private static string BuildHtmlBody(
        string title,
        string studentName,
        string leadText,
        string amountDisplay,
        string itemName,
        string reference,
        string statusLabel,
        string statusValue,
        string actionLabel,
        string footer)
    {
        StringBuilder builder = new();
        EmailTemplateBranding.AppendShellStart(builder);
        EmailTemplateBranding.AppendHeader(builder, title);
        builder.Append("<tr><td style=\"padding:30px;\">");
        builder.Append("<p style=\"font-size:16px;line-height:24px;margin:0 0 18px;color:#172033;\">Hello ")
            .Append(WebUtility.HtmlEncode(studentName))
            .Append(", ")
            .Append(WebUtility.HtmlEncode(leadText))
            .Append("</p>");
        builder.Append("<table role=\"presentation\" width=\"100%\" border=\"0\" cellspacing=\"0\" cellpadding=\"0\" style=\"border-collapse:collapse;margin:0 0 24px;\">");
        EmailTemplateBranding.AppendSummaryRow(builder, "Amount", amountDisplay, EmailTemplateBranding.PrimarySoftColor, EmailTemplateBranding.PrimaryTextColor);
        EmailTemplateBranding.AppendSummaryRow(builder, "Course/Bill", itemName);
        EmailTemplateBranding.AppendSummaryRow(builder, "Reference", reference);
        EmailTemplateBranding.AppendSummaryRow(builder, statusLabel, statusValue, "#fff7ed", "#9a3412");
        builder.Append("</table>");
        EmailTemplateBranding.AppendButton(builder, PaymentDashboardUrl, actionLabel);
        builder.Append("</td></tr>");
        EmailTemplateBranding.AppendFooter(builder, footer);
        return builder.ToString();
    }

    private static string NormalizeReason(string reason, string fallback)
        => string.IsNullOrWhiteSpace(reason) ? fallback : reason.Trim();

    private static string FormatDate(DateTime utcDate)
        => utcDate.ToString("dd MMM yyyy, HH:mm 'UTC'", CultureInfo.InvariantCulture);
}
