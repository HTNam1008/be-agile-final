using System.Globalization;
using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moe.Modules.CourseBilling.Domain.Billing;
using Moe.Modules.CourseBilling.Domain.Courses;
using Moe.Modules.CourseBilling.IGateway.Payments;
using Moe.Modules.IdentityPlatform.Domain.People;
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
    IEmailNotificationScheduler mailScheduler,
    IEmailBrandingProvider branding,
    IStudentNotificationRecipientResolver notificationRecipients,
    ISchoolAdminNotificationRecipientResolver schoolAdminRecipients,
    INotificationWriter notificationWriter,
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

        await NotifyPaymentCompletedAsync(
            enrollment.Id,
            amount,
            paidAtUtc,
            cancellationToken);
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

        Dictionary<long, decimal> paidAmountByBillId = allocations.ToDictionary(x => x.BillId, x => x.Amount);
        foreach (CourseEnrollment enrollment in enrollments)
        {
            decimal paidAmount = bills
                .Where(bill => bill.CourseEnrollmentId == enrollment.Id)
                .Sum(bill => paidAmountByBillId.GetValueOrDefault(bill.Id));

            if (paidAmount > 0m)
            {
                await NotifyPaymentCompletedAsync(
                    enrollment.Id,
                    paidAmount,
                    paidAtUtc,
                    cancellationToken);
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

    public async Task<IReadOnlyCollection<PaymentCheckoutLineItem>> BuildPaymentCheckoutLineItemsAsync(
        IReadOnlyCollection<long> billIds,
        CancellationToken cancellationToken)
    {
        long[] requestedBillIds = billIds
            .Where(id => id > 0)
            .Distinct()
            .ToArray();
        if (requestedBillIds.Length == 0) return [];

        var bills = await (
                from bill in dbContext.Set<Bill>().AsNoTracking()
                join enrollment in dbContext.Set<CourseEnrollment>().AsNoTracking()
                    on bill.CourseEnrollmentId equals enrollment.Id
                join course in dbContext.Set<Course>().AsNoTracking()
                    on enrollment.CourseId equals course.Id
                where requestedBillIds.Contains(bill.Id) && bill.OutstandingAmount > 0m
                orderby bill.CurrentDueDate, bill.SequenceNumber, bill.Id
                select new
                {
                    Bill = bill,
                    course.CourseCode,
                    course.CourseName
                })
            .ToArrayAsync(cancellationToken);
        if (bills.Length == 0) return [];

        var lineRows = await (
                from line in dbContext.Set<BillLine>().AsNoTracking()
                where requestedBillIds.Contains(line.BillId) && line.NetAmount > 0m
                orderby line.BillId, line.Id
                select new
                {
                    Line = line
                })
            .ToArrayAsync(cancellationToken);
        ILookup<long, BillLine> linesByBill = lineRows
            .Select(row => row.Line)
            .ToLookup(line => line.BillId);

        List<PaymentCheckoutLineItem> result = [];
        foreach (var row in bills)
        {
            BillLine[] lines = linesByBill[row.Bill.Id].ToArray();
            if (lines.Length == 0 || row.Bill.NetPayableAmount <= 0m)
            {
                result.Add(new PaymentCheckoutLineItem(
                    row.Bill.Id,
                    CheckoutBillName(row.CourseCode, row.CourseName),
                    $"Bill {row.Bill.BillNumber}",
                    row.Bill.OutstandingAmount));
                continue;
            }

            decimal remaining = row.Bill.OutstandingAmount;
            for (int index = 0; index < lines.Length; index++)
            {
                BillLine line = lines[index];
                decimal amount = index == lines.Length - 1
                    ? remaining
                    : decimal.Round(
                        row.Bill.OutstandingAmount * line.NetAmount / row.Bill.NetPayableAmount,
                        2,
                        MidpointRounding.AwayFromZero);
                remaining = decimal.Round(remaining - amount, 2, MidpointRounding.AwayFromZero);
                if (amount <= 0m) continue;

                result.Add(new PaymentCheckoutLineItem(
                    row.Bill.Id,
                    CheckoutLineName(line.DescriptionSnapshot),
                    CheckoutBillName(row.CourseCode, row.CourseName),
                    amount));
            }
        }

        return result;
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

    private static string CheckoutBillName(string courseCode, string courseName)
        => string.IsNullOrWhiteSpace(courseCode)
            ? courseName.Trim()
            : $"{courseCode.Trim()} - {courseName.Trim()}";

    private static string CheckoutLineName(string description)
    {
        string value = description.Trim();
        int installmentIndex = value.IndexOf(" installment ", StringComparison.OrdinalIgnoreCase);
        return installmentIndex > 0
            ? value[..installmentIndex].Trim()
            : value;
    }

    private async Task SendCourseEnrollmentSuccessEmailAsync(
        CourseEnrollment enrollment,
        string leadText,
        string footer,
        CancellationToken cancellationToken)
    {
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

        await mailScheduler.EnqueueForPersonAsync(
            "NOTI-03",
            enrollment.PersonId,
            subject,
            plainTextBody,
            htmlBody,
            "CourseEnrollment",
            enrollment.Id.ToString(CultureInfo.InvariantCulture),
            cancellationToken);
    }

    private async Task SendPaymentFailedEmailAsync(
        Bill bill,
        CourseEnrollment enrollment,
        string failureReason,
        CancellationToken cancellationToken)
    {
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
        string amountDisplay = EmailTemplateBranding.FormatMoney(bill.OutstandingAmount);
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

        await mailScheduler.EnqueueForPersonAsync(
            "NOTI-09",
            enrollment.PersonId,
            subject,
            plainTextBody,
            htmlBody,
            "Bill",
            bill.Id.ToString(CultureInfo.InvariantCulture),
            cancellationToken);
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
        StringBuilder builder = new();
        EmailTemplateBranding.AppendShellStart(builder);
        EmailTemplateBranding.AppendHeader(builder, "Action required: payment failed", appName);
        builder.Append("<tr><td style=\"padding:30px;\">");
        builder.Append("<p style=\"font-size:16px;line-height:24px;margin:0 0 18px;color:#172033;\">Hello ")
            .Append(encodedStudentName)
            .Append(", your payment could not be processed.</p>");
        builder.Append("<table role=\"presentation\" width=\"100%\" border=\"0\" cellspacing=\"0\" cellpadding=\"0\" style=\"border-collapse:collapse;margin:0 0 24px;\">");
        EmailTemplateBranding.AppendSummaryRow(builder, "Amount", amountDisplay, EmailTemplateBranding.PrimarySoftColor, EmailTemplateBranding.PrimaryTextColor);
        EmailTemplateBranding.AppendSummaryRow(builder, "Course/Bill", itemName);
        EmailTemplateBranding.AppendSummaryRow(builder, "Reason", failureReason, "#fff7ed", "#9a3412");
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
        EmailTemplateBranding.AppendSummaryRow(builder, "Course", courseName, EmailTemplateBranding.PrimarySoftColor, EmailTemplateBranding.PrimaryTextColor);
        EmailTemplateBranding.AppendSummaryRow(builder, "Course Start Date", startDateDisplay);
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

    private async Task NotifyPaymentCompletedAsync(
        long enrollmentId,
        decimal paidAmount,
        DateTime paidAtUtc,
        CancellationToken cancellationToken)
    {
        var row = await (
            from enrollment in dbContext.Set<CourseEnrollment>().AsNoTracking()
            join course in dbContext.Set<Course>().AsNoTracking()
                on enrollment.CourseId equals course.Id
            join person in dbContext.Set<Person>().AsNoTracking()
                on enrollment.PersonId equals person.Id
            where enrollment.Id == enrollmentId
            select new
            {
                enrollment.PersonId,
                course.OrganizationId,
                course.CourseCode,
                course.CourseName,
                person.OfficialFullName
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (row is null)
        {
            return;
        }

        long? studentUserAccountId = await notificationRecipients.FindUserAccountIdByPersonIdAsync(row.PersonId, cancellationToken);
        IReadOnlyCollection<long> schoolAdminUserAccountIds = await schoolAdminRecipients.FindUserAccountIdsByOrganizationIdAsync(
            row.OrganizationId,
            cancellationToken);

        if (studentUserAccountId is null && schoolAdminUserAccountIds.Count == 0)
        {
            return;
        }

        string studentName = string.IsNullOrWhiteSpace(row.OfficialFullName) ? "Student" : row.OfficialFullName.Trim();
        string title = $"Payment Completed: {row.CourseCode}";
        string studentBody = $"Payment of {paidAmount:N2} for {row.CourseName} was completed at {paidAtUtc:yyyy-MM-dd HH:mm}.";

        if (studentUserAccountId is not null)
        {
            Result<long> result = await notificationWriter.CreateAsync(
                new NotificationCreateRequest(
                    studentUserAccountId.Value,
                    NotificationTypeCode.PaymentSuccess,
                    title,
                    studentBody),
                cancellationToken);

            if (result.IsFailure)
            {
                logger.LogWarning(
                    "Payment success notification failed for student. EnrollmentId={EnrollmentId} UserAccountId={UserAccountId} Error={ErrorCode}",
                    enrollmentId,
                    studentUserAccountId.Value,
                    result.Error.Code);
            }
        }

        foreach (long schoolAdminUserAccountId in schoolAdminUserAccountIds.Distinct())
        {
            Result<long> result = await notificationWriter.CreateAsync(
                new NotificationCreateRequest(
                    schoolAdminUserAccountId,
                    NotificationTypeCode.PaymentSuccess,
                    title,
                    $"Student {studentName} completed payment of {paidAmount:N2} for {row.CourseName} at {paidAtUtc:yyyy-MM-dd HH:mm}."),
                cancellationToken);

            if (result.IsFailure)
            {
                logger.LogWarning(
                    "Payment success notification failed for school admin. EnrollmentId={EnrollmentId} UserAccountId={UserAccountId} Error={ErrorCode}",
                    enrollmentId,
                    schoolAdminUserAccountId,
                    result.Error.Code);
            }
        }
    }
}
