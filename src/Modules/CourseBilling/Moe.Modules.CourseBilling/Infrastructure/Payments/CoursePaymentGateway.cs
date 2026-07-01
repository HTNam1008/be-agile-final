using System.Globalization;
using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moe.Modules.CourseBilling.Domain.Billing;
using Moe.Modules.CourseBilling.Domain.Courses;
using Moe.Modules.CourseBilling.IGateway.Payments;
using Moe.Modules.IdentityPlatform.Domain.People;
using Moe.Modules.MailDelivery.IGateway;
using Moe.Modules.MailDelivery.Templates;
using Moe.SharedKernel.Results;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.CourseBilling.Infrastructure.Payments;

internal sealed class CoursePaymentGateway(
    MoeDbContext dbContext,
    IEmailNotificationQueue mailQueue,
    IEmailDeliverySwitch mailSwitch,
    IEmailBrandingProvider branding,
    ILogger<CoursePaymentGateway> logger) : ICoursePaymentGateway
{
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

        if (ShouldSendSelfJoinFullPaymentEnrollmentSuccessEmail(
                enrollment,
                previousStatus,
                enrollmentBills.Length))
        {
            await SendCourseEnrollmentSuccessEmailAsync(
                enrollment,
                "is confirmed and your payment has been received.",
                $"This message was sent by {branding.AppName} after your course payment was received.",
                cancellationToken);
        }
    }

    public async Task SendInstallmentEnrollmentConfirmationAsync(
        long courseEnrollmentId,
        CancellationToken cancellationToken)
    {
        CourseEnrollment? enrollment = await dbContext.Set<CourseEnrollment>()
            .SingleOrDefaultAsync(candidate => candidate.Id == courseEnrollmentId, cancellationToken);
        if (enrollment is null ||
            enrollment.EnrollmentSourceCode != CourseEnrollmentSourceCodes.SelfJoin)
        {
            return;
        }

        await SendCourseEnrollmentSuccessEmailAsync(
            enrollment,
            "is confirmed. Your installment bills will be available in the payment dashboard.",
            $"This message was sent by {branding.AppName} after your installment course enrolment was confirmed.",
            cancellationToken);
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

    private static bool ShouldSendSelfJoinFullPaymentEnrollmentSuccessEmail(
        CourseEnrollment enrollment,
        string previousStatus,
        int enrollmentBillCount)
        => enrollment.EnrollmentSourceCode == CourseEnrollmentSourceCodes.SelfJoin
            && enrollmentBillCount == 1
            && previousStatus != CourseEnrollmentStatusCodes.PaidInFull
            && enrollment.EnrollmentStatusCode == CourseEnrollmentStatusCodes.PaidInFull;

    private async Task SendCourseEnrollmentSuccessEmailAsync(
        CourseEnrollment enrollment,
        string leadText,
        string footer,
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
        string courseUrl = branding.CourseDetailUrl(course.Id);

        string plainTextBody = string.Join(Environment.NewLine, [
            branding.AppName,
            "Course enrollment confirmation",
            string.Empty,
            $"Hello {studentName}, your enrolment in {courseName} {leadText}",
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
            courseUrl,
            leadText,
            footer,
            branding.AppName);

        try
        {
            Result result = await mailQueue.EnqueueAsync(
                EmailNotificationJob.ForPerson(
                    "NOTI-03",
                    enrollment.PersonId,
                    subject,
                    plainTextBody,
                    htmlBody,
                    "CourseEnrollment",
                    enrollment.Id.ToString(CultureInfo.InvariantCulture)),
                cancellationToken);

            if (result.IsFailure)
            {
                logger.LogWarning(
                    "Course enrollment success email enqueue failed. CourseEnrollmentId={CourseEnrollmentId} ErrorCode={ErrorCode}",
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
            branding.AppName,
            "Payment failed",
            string.Empty,
            $"Hello {studentName}, your payment of {amountDisplay} for {itemName} could not be processed.",
            $"Reason: {reason}.",
            string.Empty,
            $"Retry Payment -> {branding.PaymentDashboardUrl}"
        ]);
        string htmlBody = BuildPaymentFailedHtmlBody(studentName, amountDisplay, itemName, reason, branding.AppName, branding.PaymentDashboardUrl);

        try
        {
            Result result = await mailQueue.EnqueueAsync(
                EmailNotificationJob.ForPerson(
                    "NOTI-09",
                    enrollment.PersonId,
                    subject,
                    plainTextBody,
                    htmlBody,
                    "Bill",
                    bill.Id.ToString(CultureInfo.InvariantCulture)),
                cancellationToken);

            if (result.IsFailure)
            {
                logger.LogWarning(
                    "Payment failure email enqueue failed. BillId={BillId} ErrorCode={ErrorCode}",
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
        string failureReason,
        string appName,
        string paymentDashboardUrl)
    {
        string encodedStudentName = WebUtility.HtmlEncode(studentName);
        string encodedAmount = WebUtility.HtmlEncode(amountDisplay);
        string encodedItemName = WebUtility.HtmlEncode(itemName);
        string encodedReason = WebUtility.HtmlEncode(failureReason);

        StringBuilder builder = new();
        EmailTemplateBranding.AppendShellStart(builder);
        EmailTemplateBranding.AppendHeader(builder, "Action required: payment failed", appName);
        builder.Append("<tr><td style=\"padding:30px;\">");
        builder.Append("<p style=\"font-size:16px;line-height:24px;margin:0 0 18px;color:#172033;\">Hello ")
            .Append(encodedStudentName)
            .Append(", your payment could not be processed.</p>");
        builder.Append("<table role=\"presentation\" width=\"100%\" border=\"0\" cellspacing=\"0\" cellpadding=\"0\" style=\"border-collapse:collapse;margin:0 0 24px;\">");
        AppendSummaryRow(builder, "Amount", encodedAmount, EmailTemplateBranding.PrimarySoftColor, EmailTemplateBranding.PrimaryTextColor);
        AppendSummaryRow(builder, "Course/Bill", encodedItemName, "#f8fafc", "#334155");
        AppendSummaryRow(builder, "Reason", encodedReason, "#fff7ed", "#9a3412");
        builder.Append("</table>");
        EmailTemplateBranding.AppendButton(builder, paymentDashboardUrl, "Retry Payment");
        builder.Append("</td></tr>");
        EmailTemplateBranding.AppendFooter(builder, $"This message was sent by {appName} after a failed payment attempt.");
        return builder.ToString();
    }

    private static string BuildCourseEnrollmentSuccessHtmlBody(
        string studentName,
        string courseName,
        string startDateDisplay,
        string courseUrl,
        string leadText,
        string footer,
        string appName)
    {
        string encodedStudentName = WebUtility.HtmlEncode(studentName);
        string encodedCourseName = WebUtility.HtmlEncode(courseName);
        string encodedStartDate = WebUtility.HtmlEncode(startDateDisplay);

        StringBuilder builder = new();
        EmailTemplateBranding.AppendShellStart(builder);
        EmailTemplateBranding.AppendHeader(builder, $"You're enrolled in {courseName}", appName);
        builder.Append("<tr><td style=\"padding:30px;\">");
        builder.Append("<p style=\"font-size:16px;line-height:24px;margin:0 0 18px;color:#172033;\">Hello ")
            .Append(encodedStudentName)
            .Append(", your enrolment in <strong>")
            .Append(encodedCourseName)
            .Append("</strong> ")
            .Append(WebUtility.HtmlEncode(leadText))
            .Append("</p>");
        builder.Append("<table role=\"presentation\" width=\"100%\" border=\"0\" cellspacing=\"0\" cellpadding=\"0\" style=\"border-collapse:collapse;margin:0 0 24px;\">");
        AppendSummaryRow(builder, "Course", encodedCourseName, EmailTemplateBranding.PrimarySoftColor, EmailTemplateBranding.PrimaryTextColor);
        AppendSummaryRow(builder, "Course Start Date", encodedStartDate, "#f8fafc", "#334155");
        builder.Append("</table>");
        builder.Append("<p style=\"font-size:15px;line-height:23px;margin:0 0 24px;color:#46566d;\">You can view the course details and start preparing from your Dashboard.</p>");
        EmailTemplateBranding.AppendButton(builder, courseUrl, "View Course");
        builder.Append("</td></tr>");
        builder.Append("<tr><td bgcolor=\"#f8fafc\" style=\"background-color:#f8fafc;padding:18px 30px;color:#64748b;font-size:12px;line-height:18px;\">")
            .Append(WebUtility.HtmlEncode(footer))
            .Append("</td></tr>");
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
}
