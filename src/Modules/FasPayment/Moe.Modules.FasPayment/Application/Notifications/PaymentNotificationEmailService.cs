using System.Globalization;
using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moe.Modules.CourseBilling.Domain.Billing;
using Moe.Modules.CourseBilling.Domain.Courses;
using Moe.Modules.CourseBilling.IGateway.Payments;
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
    IEmailBrandingProvider branding,
    ILogger<PaymentNotificationEmailService> logger)
{
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
            footer: $"This message was sent by {branding.AppName} after a failed payment attempt.",
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
    {
        PaymentReceiptContext receipt = await ResolvePaymentReceiptContextAsync(payment, cancellationToken);
        if (receipt.IsInstallment)
        {
            await SendPaymentStatusAsync(
                payment,
                notificationType: "NOTI-12-INSTALLMENT",
                subject: "Installment Payment Received",
                title: "Installment payment received",
                leadText: "we have received your installment payment.",
                actionLabel: "View Payments",
                footer: $"This message was sent by {branding.AppName} after a completed installment payment.",
                statusLabel: "Paid On",
                statusValue: FormatDate(completedAtUtc),
                cancellationToken: cancellationToken,
                itemNameOverride: receipt.ItemName);
            return;
        }

        await SendPaymentStatusAsync(
            payment,
            notificationType: "NOTI-12-FULL",
            subject: "Full Payment Received",
            title: "Full payment received",
            leadText: "we have received your full payment.",
            actionLabel: "View Payments",
            footer: $"This message was sent by {branding.AppName} after a completed full payment.",
            statusLabel: "Paid On",
            statusValue: FormatDate(completedAtUtc),
            cancellationToken: cancellationToken,
            itemNameOverride: receipt.ItemName);
    }

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
            footer: $"This message was sent by {branding.AppName} after a payment attempt was cancelled.",
            statusLabel: "Cancelled On",
            statusValue: FormatDate(cancelledAtUtc),
            cancellationToken);
    }

    public async Task SendPaymentDeferredAsync(
        long personId,
        long statementId,
        IReadOnlyCollection<PayableStatementBill> selectedBills,
        DateTime deferredAtUtc,
        CancellationToken cancellationToken)
    {
        if (selectedBills.Count == 0) return;
        const string notificationType = "NOTI-14-DEFERRED";
        const string subject = "Installment Payment Deferred";

        if (!mailSwitch.IsEnabled)
        {
            logger.LogInformation(
                "Payment defer email skipped because MailDelivery is disabled. PersonId={PersonId} StatementId={StatementId}",
                personId,
                statementId);
            return;
        }

        Person? person = await dbContext.Set<Person>()
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == personId, cancellationToken);
        string studentName = string.IsNullOrWhiteSpace(person?.OfficialFullName)
            ? "Student"
            : person.OfficialFullName.Trim();
        decimal totalAmount = selectedBills.Sum(bill => bill.OutstandingAmount);
        DateOnly earliestOriginalDueDate = selectedBills.Min(bill => bill.CurrentDueDate);
        DateOnly earliestNewDueDate = earliestOriginalDueDate.AddMonths(1);
        string totalDisplay = EmailTemplateBranding.FormatMoney(totalAmount);

        string[] billLines = selectedBills
            .OrderBy(bill => bill.CurrentDueDate)
            .ThenBy(bill => bill.BillId)
            .Select(bill =>
            {
                string itemName = FormatBillName(bill);
                return $"- {itemName}: {EmailTemplateBranding.FormatMoney(bill.OutstandingAmount)}, due {FormatDate(bill.CurrentDueDate)} -> {FormatDate(bill.CurrentDueDate.AddMonths(1))}";
            })
            .ToArray();

        string plainTextBody = string.Join(Environment.NewLine, [
            branding.AppName,
            subject,
            string.Empty,
            $"Hello {studentName}, your installment payment has been deferred.",
            string.Empty,
            $"Billing Statement: {statementId}",
            $"Total Deferred Amount: {totalDisplay}",
            $"Deferred Bills: {selectedBills.Count}",
            $"Earliest Original Due Date: {FormatDate(earliestOriginalDueDate)}",
            $"Earliest New Due Date: {FormatDate(earliestNewDueDate)}",
            $"Deferred On: {FormatDate(deferredAtUtc)}",
            string.Empty,
            "Deferred bill details:",
            .. billLines,
            string.Empty,
            $"View Payments -> {branding.PaymentDashboardUrl}",
            string.Empty,
            $"This message was sent by {branding.AppName} after your installment payment was deferred."
        ]);
        string htmlBody = BuildPaymentDeferredHtmlBody(
            studentName,
            statementId,
            selectedBills,
            totalDisplay,
            earliestOriginalDueDate,
            earliestNewDueDate,
            deferredAtUtc,
            branding.AppName,
            branding.PaymentDashboardUrl);

        await EnqueueForPersonAsync(
            notificationType,
            personId,
            subject,
            plainTextBody,
            htmlBody,
            "BillingStatement",
            statementId.ToString(CultureInfo.InvariantCulture),
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
        CancellationToken cancellationToken,
        string? itemNameOverride = null)
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
        string itemName = itemNameOverride ?? (payment.BillingStatementId is long statementId
            ? $"Monthly billing statement {statementId}"
            : $"Bill {payment.BillId}");
        string reference = string.IsNullOrWhiteSpace(payment.ReceiptNumber)
            ? payment.PaymentNumber
            : payment.ReceiptNumber;

        string plainTextBody = string.Join(Environment.NewLine, [
            branding.AppName,
            subject,
            string.Empty,
            $"Hello {studentName}, {leadText}",
            string.Empty,
            $"Amount: {amountDisplay}",
            $"Course/Bill: {itemName}",
            $"Reference: {reference}",
            $"{statusLabel}: {statusValue}",
            string.Empty,
            $"{actionLabel} -> {branding.PaymentDashboardUrl}",
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
            footer,
            branding.AppName,
            branding.PaymentDashboardUrl);

        await EnqueueAsync(payment, notificationType, subject, plainTextBody, htmlBody, cancellationToken);
    }

    private async Task<PaymentReceiptContext> ResolvePaymentReceiptContextAsync(
        Payment payment,
        CancellationToken cancellationToken)
    {
        if (payment.BillingStatementId is long statementId && payment.Id > 0)
        {
            PaymentReceiptBill[] bills = await (
                    from allocation in dbContext.Set<PaymentAllocation>().AsNoTracking()
                    join bill in dbContext.Set<Bill>().AsNoTracking() on allocation.BillId equals bill.Id
                    join enrollment in dbContext.Set<CourseEnrollment>().AsNoTracking() on bill.CourseEnrollmentId equals enrollment.Id
                    join course in dbContext.Set<Course>().AsNoTracking() on enrollment.CourseId equals course.Id
                    where allocation.PaymentId == payment.Id
                    select new PaymentReceiptBill(
                        bill.Id,
                        bill.CourseEnrollmentId,
                        bill.SequenceNumber,
                        course.CourseCode,
                        course.CourseName))
                .ToArrayAsync(cancellationToken);

            if (bills.Length > 0)
            {
                long[] enrollmentIds = bills.Select(x => x.CourseEnrollmentId).Distinct().ToArray();
                int installmentEnrollmentCount = await dbContext.Set<Bill>()
                    .AsNoTracking()
                    .Where(x => enrollmentIds.Contains(x.CourseEnrollmentId))
                    .GroupBy(x => x.CourseEnrollmentId)
                    .CountAsync(group => group.Count() > 1, cancellationToken);
                bool installment = installmentEnrollmentCount > 0 || bills.Any(x => x.SequenceNumber > 1);
                string itemName = bills.Select(x => x.CourseName).Distinct().Count() == 1
                    ? bills[0].CourseName
                    : $"Monthly billing statement {statementId}";
                if (bills.Length > 1)
                    itemName = $"{itemName} ({bills.Length} bill items)";
                return new PaymentReceiptContext(installment, itemName);
            }

            return new PaymentReceiptContext(IsInstallment: true, $"Monthly billing statement {statementId}");
        }

        if (payment.BillId > 0)
        {
            PaymentReceiptBill? bill = await (
                    from candidate in dbContext.Set<Bill>().AsNoTracking()
                    join enrollment in dbContext.Set<CourseEnrollment>().AsNoTracking() on candidate.CourseEnrollmentId equals enrollment.Id
                    join course in dbContext.Set<Course>().AsNoTracking() on enrollment.CourseId equals course.Id
                    where candidate.Id == payment.BillId
                    select new PaymentReceiptBill(
                        candidate.Id,
                        candidate.CourseEnrollmentId,
                        candidate.SequenceNumber,
                        course.CourseCode,
                        course.CourseName))
                .SingleOrDefaultAsync(cancellationToken);

            if (bill is not null)
            {
                int billCount = await dbContext.Set<Bill>()
                    .AsNoTracking()
                    .CountAsync(x => x.CourseEnrollmentId == bill.CourseEnrollmentId, cancellationToken);
                bool installment = billCount > 1 || bill.SequenceNumber > 1 || payment.InstallmentNumber > 0;
                string itemName = installment
                    ? $"{bill.CourseName} - installment {Math.Max(1, bill.SequenceNumber)}"
                    : bill.CourseName;
                return new PaymentReceiptContext(installment, itemName);
            }
        }

        return new PaymentReceiptContext(
            payment.BillingStatementId is not null || payment.InstallmentNumber > 0,
            payment.BillingStatementId is long fallbackStatementId
                ? $"Monthly billing statement {fallbackStatementId}"
                : $"Bill {payment.BillId}");
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
            Result result = await EnqueueJobAsync(
                notificationType,
                payment.PayerPersonId,
                subject,
                plainTextBody,
                htmlBody,
                "Payment",
                entityId,
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

    private async Task EnqueueForPersonAsync(
        string notificationType,
        long personId,
        string subject,
        string plainTextBody,
        string htmlBody,
        string entityType,
        string entityId,
        CancellationToken cancellationToken)
    {
        try
        {
            Result result = await EnqueueJobAsync(
                notificationType,
                personId,
                subject,
                plainTextBody,
                htmlBody,
                entityType,
                entityId,
                cancellationToken);

            if (result.IsFailure)
            {
                logger.LogWarning(
                    "Payment notification email enqueue failed. NotificationType={NotificationType} EntityType={EntityType} EntityId={EntityId} ErrorCode={ErrorCode}",
                    notificationType,
                    entityType,
                    entityId,
                    result.Error.Code);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(
                ex,
                "Payment notification email threw an exception. NotificationType={NotificationType} EntityType={EntityType} EntityId={EntityId}",
                notificationType,
                entityType,
                entityId);
        }
    }

    private async Task<Result> EnqueueJobAsync(
        string notificationType,
        long personId,
        string subject,
        string plainTextBody,
        string htmlBody,
        string entityType,
        string entityId,
        CancellationToken cancellationToken)
        => await mailQueue.EnqueueAsync(
            EmailNotificationJob.ForPerson(
                notificationType,
                personId,
                subject,
                plainTextBody,
                htmlBody,
                entityType,
                entityId),
            cancellationToken);

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
        string footer,
        string appName,
        string paymentDashboardUrl)
    {
        StringBuilder builder = new();
        EmailTemplateBranding.AppendShellStart(builder);
        EmailTemplateBranding.AppendHeader(builder, title, appName);
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
        EmailTemplateBranding.AppendButton(builder, paymentDashboardUrl, actionLabel);
        builder.Append("</td></tr>");
        EmailTemplateBranding.AppendFooter(builder, footer);
        return builder.ToString();
    }

    private static string BuildPaymentDeferredHtmlBody(
        string studentName,
        long statementId,
        IReadOnlyCollection<PayableStatementBill> selectedBills,
        string totalDisplay,
        DateOnly earliestOriginalDueDate,
        DateOnly earliestNewDueDate,
        DateTime deferredAtUtc,
        string appName,
        string paymentDashboardUrl)
    {
        StringBuilder builder = new();
        EmailTemplateBranding.AppendShellStart(builder);
        EmailTemplateBranding.AppendHeader(builder, "Installment payment deferred", appName);
        builder.Append("<tr><td style=\"padding:30px;\">");
        builder.Append("<p style=\"font-size:16px;line-height:24px;margin:0 0 18px;color:#172033;\">Hello ")
            .Append(WebUtility.HtmlEncode(studentName))
            .Append(", your installment payment has been deferred.</p>");
        builder.Append("<table role=\"presentation\" width=\"100%\" border=\"0\" cellspacing=\"0\" cellpadding=\"0\" style=\"border-collapse:collapse;margin:0 0 24px;\">");
        EmailTemplateBranding.AppendSummaryRow(builder, "Total Deferred Amount", totalDisplay, EmailTemplateBranding.PrimarySoftColor, EmailTemplateBranding.PrimaryTextColor);
        EmailTemplateBranding.AppendSummaryRow(builder, "Billing Statement", statementId.ToString(CultureInfo.InvariantCulture));
        EmailTemplateBranding.AppendSummaryRow(builder, "Deferred Bills", selectedBills.Count.ToString(CultureInfo.InvariantCulture));
        EmailTemplateBranding.AppendSummaryRow(builder, "Original Due Date", FormatDate(earliestOriginalDueDate), "#fff7ed", "#9a3412");
        EmailTemplateBranding.AppendSummaryRow(builder, "New Due Date", FormatDate(earliestNewDueDate), "#ecfdf5", "#047857");
        EmailTemplateBranding.AppendSummaryRow(builder, "Deferred On", FormatDate(deferredAtUtc));
        builder.Append("</table>");
        builder.Append("<table role=\"presentation\" width=\"100%\" border=\"0\" cellspacing=\"0\" cellpadding=\"0\" style=\"border-collapse:collapse;margin:0 0 24px;\">");
        foreach (PayableStatementBill bill in selectedBills.OrderBy(bill => bill.CurrentDueDate).ThenBy(bill => bill.BillId))
        {
            builder.Append("<tr><td style=\"padding:12px 0;border-bottom:1px solid #e2e8f0;\">");
            builder.Append("<div style=\"font-size:14px;line-height:20px;color:#172033;font-weight:bold;\">")
                .Append(WebUtility.HtmlEncode(FormatBillName(bill)))
                .Append("</div>");
            builder.Append("<div style=\"font-size:13px;line-height:19px;color:#64748b;\">")
                .Append(WebUtility.HtmlEncode(EmailTemplateBranding.FormatMoney(bill.OutstandingAmount)))
                .Append(" · Due ")
                .Append(WebUtility.HtmlEncode(FormatDate(bill.CurrentDueDate)))
                .Append(" -> ")
                .Append(WebUtility.HtmlEncode(FormatDate(bill.CurrentDueDate.AddMonths(1))))
                .Append("</div>");
            builder.Append("</td></tr>");
        }
        builder.Append("</table>");
        EmailTemplateBranding.AppendButton(builder, paymentDashboardUrl, "View Payments");
        builder.Append("</td></tr>");
        EmailTemplateBranding.AppendFooter(builder, $"This message was sent by {appName} after your installment payment was deferred.");
        return builder.ToString();
    }

    private static string NormalizeReason(string reason, string fallback)
        => string.IsNullOrWhiteSpace(reason) ? fallback : reason.Trim();

    private static string FormatDate(DateTime utcDate)
        => utcDate.ToString("dd MMM yyyy, HH:mm 'UTC'", CultureInfo.InvariantCulture);

    private static string FormatDate(DateOnly date)
        => date.ToString("dd MMM yyyy", CultureInfo.InvariantCulture);

    private static string FormatBillName(PayableStatementBill bill)
        => string.IsNullOrWhiteSpace(bill.CourseName)
            ? $"Bill {bill.BillId}"
            : bill.CourseName.Trim();

    private sealed record PaymentReceiptContext(bool IsInstallment, string ItemName);

    private sealed record PaymentReceiptBill(
        long BillId,
        long CourseEnrollmentId,
        int SequenceNumber,
        string CourseCode,
        string CourseName);
}
