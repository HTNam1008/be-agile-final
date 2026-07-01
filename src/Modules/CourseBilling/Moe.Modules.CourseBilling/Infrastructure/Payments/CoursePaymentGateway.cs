using System.Globalization;
using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moe.Modules.CourseBilling.Domain.Billing;
using Moe.Modules.CourseBilling.Domain.Courses;
using Moe.Modules.CourseBilling.IGateway.Payments;
using Moe.Modules.IdentityPlatform.Domain.People;
using Moe.Modules.IdentityPlatform.IGateway.People;
using Moe.Modules.IdentityPlatform.IGateway.Students;
using Moe.Modules.MailDelivery.IGateway;
using Moe.Modules.MailDelivery.Templates;
using Moe.Modules.Notifications.Domain.Notifications;
using Moe.Modules.Notifications.IGateway.Notifications;
using Moe.SharedKernel.Results;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.CourseBilling.Infrastructure.Payments;

internal sealed class CoursePaymentGateway(
    MoeDbContext dbContext,
    IEmailRecipientResolver recipientResolver,
    IEmailDeliveryGateway mailGateway,
    IStudentNotificationRecipientResolver notificationRecipients,
    INotificationWriter notificationWriter,
    IEmailDeliverySwitch mailSwitch,
    ILogger<CoursePaymentGateway> logger) : ICoursePaymentGateway
{
    private const string CourseDetailUrl = "http://localhost:5173/portal/courses";
    private const string PaymentDashboardUrl = "http://localhost:5173/portal/payments";

    public Task<PayableCourseBill?> FindPayableBillAsync(
        long billId,
        long personId,
        CancellationToken cancellationToken)
    {
        return (
            from bill in dbContext.Set<Bill>().AsNoTracking()
            join enrollment in dbContext.Set<CourseEnrollment>().AsNoTracking()
                on bill.CourseEnrollmentId equals enrollment.Id
            join course in dbContext.Set<Course>().AsNoTracking()
                on enrollment.CourseId equals course.Id
            where bill.Id == billId
                && enrollment.PersonId == personId
                && bill.OutstandingAmount > 0m
                && bill.BillStatusCode != BillStatusCodes.Cancelled
            select new PayableCourseBill(
                bill.Id,
                enrollment.Id,
                course.Id,
                enrollment.PersonId,
                course.OrganizationId,
                course.CourseCode,
                course.CourseName,
                bill.OutstandingAmount,
                bill.BillStatusCode))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public Task<long?> FindCourseOrganizationIdAsync(long courseId, CancellationToken cancellationToken)
        => dbContext.Set<Course>()
            .AsNoTracking()
            .Where(course => course.Id == courseId)
            .Select(course => (long?)course.OrganizationId)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task ApplySuccessfulPaymentAsync(
        long billId,
        decimal amount,
        bool paidInFull,
        DateTime paidAtUtc,
        CancellationToken cancellationToken)
    {
        Bill bill = await dbContext.Set<Bill>()
            .SingleAsync(candidate => candidate.Id == billId, cancellationToken);
        CourseEnrollment enrollment = await dbContext.Set<CourseEnrollment>()
            .SingleAsync(candidate => candidate.Id == bill.CourseEnrollmentId, cancellationToken);
        string previousStatus = enrollment.EnrollmentStatusCode;

        var result = bill.RecordPayment(amount, paidAtUtc);
        if (result.IsFailure) throw new InvalidOperationException(result.Error.Message);
        Bill[] enrollmentBills = await dbContext.Set<Bill>()
            .Where(candidate => candidate.CourseEnrollmentId == enrollment.Id)
            .ToArrayAsync(cancellationToken);
        bool allBillsPaid = enrollmentBills.All(candidate =>
            candidate.BillStatusCode == BillStatusCodes.Paid ||
            candidate.BillStatusCode == BillStatusCodes.Cancelled);
        enrollment.GrantPaidAccess(allBillsPaid);

        if (ShouldSendSelfJoinEnrollmentSuccessEmail(enrollment, previousStatus))
        {
            await SendCourseEnrollmentSuccessEmailAsync(enrollment, cancellationToken);
        }
    }

    public async Task ApplyPaymentFailureAsync(
        long billId,
        string failureReason,
        CancellationToken cancellationToken)
    {
        Bill bill = await dbContext.Set<Bill>()
            .SingleAsync(candidate => candidate.Id == billId, cancellationToken);
        CourseEnrollment enrollment = await dbContext.Set<CourseEnrollment>()
            .SingleAsync(candidate => candidate.Id == bill.CourseEnrollmentId, cancellationToken);
        enrollment.LockForPaymentFailure();
        await NotifyBillOverdueAsync(billId, cancellationToken);
        await SendPaymentFailedEmailAsync(bill, enrollment, failureReason, cancellationToken);
    }

    public async Task ApplyFullRefundAsync(long billId, DateTime refundedAtUtc, CancellationToken cancellationToken)
    {
        long? enrollmentId = await dbContext.Set<Bill>()
            .Where(bill => bill.Id == billId)
            .Select(bill => (long?)bill.CourseEnrollmentId)
            .SingleOrDefaultAsync(cancellationToken);

        if (enrollmentId is null)
        {
            return;
        }

        await MarkEnrollmentsRefundedAsync([enrollmentId.Value], refundedAtUtc, cancellationToken);
    }

    public async Task ApplyFullRefundForBillsAsync(
        IReadOnlyCollection<long> billIds,
        DateTime refundedAtUtc,
        CancellationToken cancellationToken)
    {
        if (billIds.Count == 0)
        {
            return;
        }

        long[] enrollmentIds = await dbContext.Set<Bill>()
            .Where(bill => billIds.Contains(bill.Id))
            .Select(bill => bill.CourseEnrollmentId)
            .Distinct()
            .ToArrayAsync(cancellationToken);

        await MarkEnrollmentsRefundedAsync(enrollmentIds, refundedAtUtc, cancellationToken);
    }

    public async Task<PayableStatement?> FindPayableStatementAsync(
        long statementId,
        long personId,
        CancellationToken cancellationToken)
    {
        BillingStatement? statement = await dbContext.Set<BillingStatement>()
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == statementId && x.PersonId == personId, cancellationToken);
        if (statement is null || statement.OutstandingAmount <= 0m) return null;

        PayableStatementBill[] bills = await (
            from item in dbContext.Set<BillingStatementItem>().AsNoTracking()
            join bill in dbContext.Set<Bill>().AsNoTracking() on item.BillId equals bill.Id
            join enrollment in dbContext.Set<CourseEnrollment>().AsNoTracking() on bill.CourseEnrollmentId equals enrollment.Id
            join course in dbContext.Set<Course>().AsNoTracking() on enrollment.CourseId equals course.Id
            where item.BillingStatementId == statementId
                && bill.OutstandingAmount > 0m
                && bill.BillStatusCode != BillStatusCodes.Paid
                && bill.BillStatusCode != BillStatusCodes.Cancelled
            orderby bill.CurrentDueDate, bill.OriginalDueDate, bill.Id
            select new PayableStatementBill(
                item.Id,
                bill.Id,
                course.OrganizationId,
                bill.OutstandingAmount,
                bill.CurrentDueDate,
                bill.OriginalDueDate,
                dbContext.Set<Bill>().Count(candidate => candidate.CourseEnrollmentId == enrollment.Id) > 1,
                course.CourseCode,
                course.CourseName))
            .ToArrayAsync(cancellationToken);
        decimal total = bills.Sum(x => x.OutstandingAmount);
        return total <= 0m ? null : new(statement.Id, personId, total, statement.CurrencyCode, bills);
    }

    public async Task ApplyStatementPaymentAsync(
        long statementId,
        IReadOnlyCollection<BillPaymentAllocation> allocations,
        DateTime paidAtUtc,
        CancellationToken cancellationToken)
    {
        BillingStatement statement = await dbContext.Set<BillingStatement>()
            .SingleAsync(x => x.Id == statementId, cancellationToken);
        long[] billIds = allocations.Select(x => x.BillId).ToArray();
        List<Bill> bills = await dbContext.Set<Bill>()
            .Where(x => billIds.Contains(x.Id))
            .ToListAsync(cancellationToken);
        foreach (BillPaymentAllocation allocation in allocations)
        {
            Bill bill = bills.Single(x => x.Id == allocation.BillId);
            var result = bill.RecordPayment(allocation.Amount, paidAtUtc);
            if (result.IsFailure) throw new InvalidOperationException(result.Error.Message);
        }
        long[] enrollmentIds = bills.Select(x => x.CourseEnrollmentId).Distinct().ToArray();
        List<CourseEnrollment> enrollments = await dbContext.Set<CourseEnrollment>()
            .Where(x => enrollmentIds.Contains(x.Id))
            .ToListAsync(cancellationToken);
        Dictionary<long, string> previousStatusByEnrollmentId = enrollments.ToDictionary(
            x => x.Id,
            x => x.EnrollmentStatusCode);
        List<Bill> enrollmentBills = await dbContext.Set<Bill>()
            .Where(x => enrollmentIds.Contains(x.CourseEnrollmentId))
            .ToListAsync(cancellationToken);
        foreach (CourseEnrollment enrollment in enrollments)
        {
            bool allBillsPaid = enrollmentBills
                .Where(x => x.CourseEnrollmentId == enrollment.Id)
                .All(x =>
                    x.BillStatusCode == BillStatusCodes.Paid ||
                    x.BillStatusCode == BillStatusCodes.Cancelled);
            enrollment.GrantPaidAccess(allBillsPaid);
        }
        foreach (CourseEnrollment enrollment in enrollments)
        {
            if (ShouldSendSelfJoinEnrollmentSuccessEmail(
                    enrollment,
                    previousStatusByEnrollmentId[enrollment.Id]))
            {
                await SendCourseEnrollmentSuccessEmailAsync(enrollment, cancellationToken);
            }
        }
        var statementBillAmounts = await (
                from item in dbContext.Set<BillingStatementItem>()
                join bill in dbContext.Set<Bill>() on item.BillId equals bill.Id
                where item.BillingStatementId == statementId
                select new
                {
                    Item = item,
                    bill.NetPayableAmount,
                    bill.OutstandingAmount
                })
            .ToListAsync(cancellationToken);

        foreach (var row in statementBillAmounts)
        {
            row.Item.Refresh(
                row.NetPayableAmount,
                row.NetPayableAmount - row.OutstandingAmount);
        }

        decimal total = statementBillAmounts.Sum(x => x.NetPayableAmount);
        decimal outstanding = statementBillAmounts.Sum(x => x.OutstandingAmount);
        statement.Refresh(total, total - outstanding, paidAtUtc);
    }

    public async Task<Result> DeferStatementAsync(
        long statementId,
        long personId,
        IReadOnlyCollection<long> billIds,
        long actorLoginAccountId,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        BillingStatement statement = await dbContext.Set<BillingStatement>()
            .SingleAsync(x => x.Id == statementId && x.PersonId == personId, cancellationToken);
        long[] requestedBillIds = billIds.Distinct().ToArray();
        List<Bill> bills = await (
            from item in dbContext.Set<BillingStatementItem>()
            join bill in dbContext.Set<Bill>() on item.BillId equals bill.Id
            where item.BillingStatementId == statementId
                && requestedBillIds.Contains(bill.Id)
                && bill.OutstandingAmount > 0m
            select bill).ToListAsync(cancellationToken);
        foreach (Bill bill in bills)
        {
            DateOnly from = bill.CurrentDueDate;
            decimal amount = bill.OutstandingAmount;
            var result = bill.DeferToNextMonth(utcNow);
            if (result.IsFailure)
                return result;

            await dbContext.Set<BillDeferral>().AddAsync(new BillDeferral(
                bill.Id,
                bill.CourseEnrollmentId,
                null,
                from,
                bill.CurrentDueDate,
                amount,
                bill.DeferralCount,
                actorLoginAccountId,
                utcNow), cancellationToken);
        }
        statement.Refresh(statement.TotalAmount, statement.PaidAmount, utcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private async Task MarkEnrollmentsRefundedAsync(
        IReadOnlyCollection<long> enrollmentIds,
        DateTime refundedAtUtc,
        CancellationToken cancellationToken)
    {
        if (enrollmentIds.Count == 0)
        {
            return;
        }

        List<CourseEnrollment> enrollments = await dbContext.Set<CourseEnrollment>()
            .Where(candidate => enrollmentIds.Contains(candidate.Id))
            .ToListAsync(cancellationToken);

        foreach (CourseEnrollment enrollment in enrollments)
        {
            if (enrollment.EnrollmentStatusCode == CourseEnrollmentStatusCodes.Refunded)
            {
                continue;
            }

            enrollment.MarkRefunded(refundedAtUtc);
        }
    }

    private static bool ShouldSendSelfJoinEnrollmentSuccessEmail(
        CourseEnrollment enrollment,
        string previousStatus)
        => enrollment.EnrollmentSourceCode == CourseEnrollmentSourceCodes.SelfJoin
            && previousStatus != CourseEnrollmentStatusCodes.PaidInFull
            && enrollment.EnrollmentStatusCode == CourseEnrollmentStatusCodes.PaidInFull;

    private async Task SendCourseEnrollmentSuccessEmailAsync(
        CourseEnrollment enrollment,
        CancellationToken cancellationToken)
    {
        if (!mailSwitch.IsEnabled)
        {
            logger.LogInformation(
                "Course enrollment email skipped because MailDelivery is disabled. PersonId={PersonId} CourseEnrollmentId={CourseEnrollmentId}",
                enrollment.PersonId,
                enrollment.Id);
            return;
        }

        EmailRecipient? recipient;
        try
        {
            recipient = await recipientResolver.ResolveForPersonAsync(enrollment.PersonId, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Course enrollment email recipient resolution failed. PersonId={PersonId} CourseEnrollmentId={CourseEnrollmentId}", enrollment.PersonId, enrollment.Id);
            return;
        }

        if (recipient is null)
        {
            logger.LogWarning("Course enrollment email skipped because no valid recipient was found. PersonId={PersonId} CourseEnrollmentId={CourseEnrollmentId}", enrollment.PersonId, enrollment.Id);
            return;
        }

        Course course = await dbContext.Set<Course>()
            .AsNoTracking()
            .SingleAsync(x => x.Id == enrollment.CourseId, cancellationToken);
        Person? person = await dbContext.Set<Person>()
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == enrollment.PersonId, cancellationToken);

        string studentName = string.IsNullOrWhiteSpace(person?.OfficialFullName)
            ? "Student"
            : person.OfficialFullName.Trim();
        string courseName = course.CourseName.Trim();
        string startDateDisplay = course.StartDate.ToString("dd MMM yyyy", CultureInfo.InvariantCulture);
        string subject = $"You're Enrolled in {courseName}";
        string courseUrl = $"{CourseDetailUrl}/{course.Id}";

        string plainTextBody = string.Join(Environment.NewLine, [
            "MOE SEEDS",
            "Course enrollment confirmation",
            string.Empty,
            $"Hello {studentName}, your enrolment in {courseName} is confirmed and your payment has been received.",
            string.Empty,
            $"Course Start Date: {startDateDisplay}",
            string.Empty,
            "You can view the course details and start preparing from your Dashboard.",
            $"View Course -> {courseUrl}"
        ]);

        string htmlBody = BuildCourseEnrollmentSuccessHtmlBody(
            studentName,
            courseName,
            startDateDisplay,
            courseUrl);

        try
        {
            Result result = await mailGateway.SendAsync(
                new EmailDeliveryMessage(recipient.EmailAddress, subject, plainTextBody, htmlBody),
                cancellationToken);

            if (result.IsFailure)
            {
                logger.LogWarning(
                    "Course enrollment success email failed. CourseEnrollmentId={CourseEnrollmentId} ErrorCode={ErrorCode}",
                    enrollment.Id,
                    result.Error.Code);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(
                ex,
                "Course enrollment success email threw an exception. CourseEnrollmentId={CourseEnrollmentId}",
                enrollment.Id);
        }
    }

    private async Task SendPaymentFailedEmailAsync(
        Bill bill,
        CourseEnrollment enrollment,
        string failureReason,
        CancellationToken cancellationToken)
    {
        if (!mailSwitch.IsEnabled)
        {
            logger.LogInformation(
                "Payment failure email skipped because MailDelivery is disabled. PersonId={PersonId} BillId={BillId}",
                enrollment.PersonId,
                bill.Id);
            return;
        }

        EmailRecipient? recipient;
        try
        {
            recipient = await recipientResolver.ResolveForPersonAsync(enrollment.PersonId, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Payment failure email recipient resolution failed. PersonId={PersonId} BillId={BillId}", enrollment.PersonId, bill.Id);
            return;
        }

        if (recipient is null)
        {
            logger.LogWarning("Payment failure email skipped because no valid recipient was found. PersonId={PersonId} BillId={BillId}", enrollment.PersonId, bill.Id);
            return;
        }

        Course course = await dbContext.Set<Course>()
            .AsNoTracking()
            .SingleAsync(x => x.Id == enrollment.CourseId, cancellationToken);
        Person? person = await dbContext.Set<Person>()
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == enrollment.PersonId, cancellationToken);

        string studentName = string.IsNullOrWhiteSpace(person?.OfficialFullName)
            ? "Student"
            : person.OfficialFullName.Trim();
        string itemName = string.IsNullOrWhiteSpace(course.CourseName)
            ? bill.BillNumber
            : course.CourseName.Trim();
        string amountDisplay = $"SGD {bill.OutstandingAmount:N2}";
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
        string htmlBody = BuildPaymentFailedHtmlBody(studentName, amountDisplay, itemName, reason);

        try
        {
            Result result = await mailGateway.SendAsync(
                new EmailDeliveryMessage(recipient.EmailAddress, subject, plainTextBody, htmlBody),
                cancellationToken);

            if (result.IsFailure)
            {
                logger.LogWarning(
                    "Payment failure email failed. BillId={BillId} ErrorCode={ErrorCode}",
                    bill.Id,
                    result.Error.Code);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Payment failure email threw an exception. BillId={BillId}", bill.Id);
        }
    }

    private static string BuildPaymentFailedHtmlBody(
        string studentName,
        string amountDisplay,
        string itemName,
        string failureReason)
    {
        string encodedStudentName = WebUtility.HtmlEncode(studentName);
        string encodedAmount = WebUtility.HtmlEncode(amountDisplay);
        string encodedItemName = WebUtility.HtmlEncode(itemName);
        string encodedReason = WebUtility.HtmlEncode(failureReason);

        StringBuilder builder = new();
        EmailTemplateBranding.AppendShellStart(builder);
        EmailTemplateBranding.AppendHeader(builder, "Action required: payment failed");
        builder.Append("<tr><td style=\"padding:30px;\">");
        builder.Append("<p style=\"font-size:16px;line-height:24px;margin:0 0 18px;color:#172033;\">Hello ")
            .Append(encodedStudentName)
            .Append(", your payment could not be processed.</p>");
        builder.Append("<table role=\"presentation\" width=\"100%\" border=\"0\" cellspacing=\"0\" cellpadding=\"0\" style=\"border-collapse:collapse;margin:0 0 24px;\">");
        AppendSummaryRow(builder, "Amount", encodedAmount, EmailTemplateBranding.PrimarySoftColor, EmailTemplateBranding.PrimaryTextColor);
        AppendSummaryRow(builder, "Course/Bill", encodedItemName, "#f8fafc", "#334155");
        AppendSummaryRow(builder, "Reason", encodedReason, "#fff7ed", "#9a3412");
        builder.Append("</table>");
        EmailTemplateBranding.AppendButton(builder, PaymentDashboardUrl, "Retry Payment");
        builder.Append("</td></tr>");
        builder.Append("<tr><td bgcolor=\"#f8fafc\" style=\"background-color:#f8fafc;padding:18px 30px;color:#64748b;font-size:12px;line-height:18px;\">This message was sent by MOE SEEDS after a failed payment attempt.</td></tr>");
        builder.Append("</table></td></tr></table></body></html>");
        return builder.ToString();
    }

    private static string BuildCourseEnrollmentSuccessHtmlBody(
        string studentName,
        string courseName,
        string startDateDisplay,
        string courseUrl)
    {
        string encodedStudentName = WebUtility.HtmlEncode(studentName);
        string encodedCourseName = WebUtility.HtmlEncode(courseName);
        string encodedStartDate = WebUtility.HtmlEncode(startDateDisplay);

        StringBuilder builder = new();
        EmailTemplateBranding.AppendShellStart(builder);
        EmailTemplateBranding.AppendHeader(builder, $"You're enrolled in {courseName}");
        builder.Append("<tr><td style=\"padding:30px;\">");
        builder.Append("<p style=\"font-size:16px;line-height:24px;margin:0 0 18px;color:#172033;\">Hello ")
            .Append(encodedStudentName)
            .Append(", your enrolment in <strong>")
            .Append(encodedCourseName)
            .Append("</strong> is confirmed and your payment has been received.</p>");
        builder.Append("<table role=\"presentation\" width=\"100%\" border=\"0\" cellspacing=\"0\" cellpadding=\"0\" style=\"border-collapse:collapse;margin:0 0 24px;\">");
        AppendSummaryRow(builder, "Course", encodedCourseName, EmailTemplateBranding.PrimarySoftColor, EmailTemplateBranding.PrimaryTextColor);
        AppendSummaryRow(builder, "Course Start Date", encodedStartDate, "#f8fafc", "#334155");
        builder.Append("</table>");
        builder.Append("<p style=\"font-size:15px;line-height:23px;margin:0 0 24px;color:#46566d;\">You can view the course details and start preparing from your Dashboard.</p>");
        EmailTemplateBranding.AppendButton(builder, courseUrl, "View Course");
        builder.Append("</td></tr>");
        builder.Append("<tr><td bgcolor=\"#f8fafc\" style=\"background-color:#f8fafc;padding:18px 30px;color:#64748b;font-size:12px;line-height:18px;\">This message was sent by MOE SEEDS after your course payment was received.</td></tr>");
        builder.Append("</table>");
        builder.Append("</td></tr></table>");
        builder.Append("</body></html>");
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
        builder.Append("<div style=\"font-size:22px;line-height:28px;color:")
            .Append(valueColor)
            .Append(";font-weight:bold;padding-top:4px;\">")
            .Append(value)
            .Append("</div>");
        builder.Append("</td></tr>");
    }

    private async Task NotifyBillOverdueAsync(long billId, CancellationToken cancellationToken)
    {
        var row = await (
            from bill in dbContext.Set<Bill>().AsNoTracking()
            join enrollment in dbContext.Set<CourseEnrollment>().AsNoTracking()
                on bill.CourseEnrollmentId equals enrollment.Id
            join course in dbContext.Set<Course>().AsNoTracking()
                on enrollment.CourseId equals course.Id
            where bill.Id == billId
            select new
            {
                enrollment.PersonId,
                bill.BillNumber,
                bill.OutstandingAmount,
                course.CourseName
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (row is null)
        {
            return;
        }

        long? userAccountId = await notificationRecipients.FindUserAccountIdByPersonIdAsync(row.PersonId, cancellationToken);
        if (userAccountId is null)
        {
            logger.LogWarning("Bill overdue notification skipped because no user account was found for person {PersonId}.", row.PersonId);
            return;
        }

        Result<long> result = await notificationWriter.CreateAsync(
            new NotificationCreateRequest(
                userAccountId.Value,
                NotificationTypeCode.BillOverdue,
                "Urgent: Bill Overdue",
                $"Bill {row.BillNumber} for {row.CourseName} is now OUTSTANDING. Please pay {row.OutstandingAmount:N2} immediately."),
            cancellationToken);

        if (result.IsFailure)
        {
            logger.LogWarning(
                "Bill overdue notification failed. PersonId={PersonId} BillId={BillId} Error={ErrorCode}",
                row.PersonId,
                billId,
                result.Error.Code);
        }
    }
}
