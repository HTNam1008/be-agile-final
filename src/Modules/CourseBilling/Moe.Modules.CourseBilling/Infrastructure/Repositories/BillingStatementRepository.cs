using System.Globalization;
using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moe.Modules.CourseBilling.Contracts.BillingStatements;
using Moe.Modules.CourseBilling.Domain.Billing;
using Moe.Modules.CourseBilling.Domain.Courses;
using Moe.Modules.IdentityPlatform.Domain.People;
using Moe.Modules.MailDelivery.IGateway;
using Moe.Modules.MailDelivery.Templates;
using Moe.SharedKernel.Results;
using Moe.Modules.CourseBilling.IGateway.Payments;
using Moe.Modules.CourseBilling.IGateway.Repositories;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.CourseBilling.Infrastructure.Repositories;

internal sealed class BillingStatementRepository(
    MoeDbContext dbContext,
    ICoursePaymentPlanGateway paymentPlans,
    IEmailNotificationQueue mailQueue,
    IEmailDeliverySwitch mailSwitch,
    IEmailBrandingProvider branding,
    ILogger<BillingStatementRepository> logger) : IBillingStatementRepository
{
    public async Task<BillingStatementResponse> GetOrCreateAsync(
        long personId,
        int year,
        int month,
        DateTime utcNow,
        BillingStatementNotificationMode notificationMode,
        CancellationToken cancellationToken)
    {
        DateOnly monthStart = new(year, month, 1);
        DateOnly monthEnd = monthStart.AddMonths(1);
        BillingStatement? statement = await dbContext.Set<BillingStatement>()
            .SingleOrDefaultAsync(x =>
                x.PersonId == personId &&
                x.StatementYear == year &&
                x.StatementMonth == month,
                cancellationToken);
        bool statementCreated = false;
        if (statement is null)
        {
            statement = BillingStatement.Create(personId, year, month, utcNow);
            await dbContext.Set<BillingStatement>().AddAsync(statement, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            statementCreated = true;
        }

        var candidateBills = await (
            from bill in dbContext.Set<Bill>()
            join enrollment in dbContext.Set<CourseEnrollment>()
                on bill.CourseEnrollmentId equals enrollment.Id
            join course in dbContext.Set<Course>()
                on enrollment.CourseId equals course.Id
            where enrollment.PersonId == personId
                && enrollment.EnrollmentStatusCode != CourseEnrollmentStatusCodes.PendingPlanSelection
                && bill.CurrentDueDate >= monthStart
                && bill.CurrentDueDate < monthEnd
                && bill.OutstandingAmount > 0m
                && bill.BillStatusCode != BillStatusCodes.Paid
                && bill.BillStatusCode != BillStatusCodes.Cancelled
            orderby bill.CurrentDueDate, bill.OriginalDueDate, bill.Id
            select new
            {
                Bill = bill,
                Course = course,
                Enrollment = enrollment
            })
            .ToListAsync(cancellationToken);

        Dictionary<long, string> planTypesById = await LoadPlanTypesAsync(
            candidateBills
                .Select(row => row.Enrollment.CoursePaymentPlanId)
                .OfType<long>()
                .Distinct()
                .ToArray(),
            cancellationToken);
        var bills = candidateBills
            .Select(row =>
            {
                string planTypeCode = row.Enrollment.CoursePaymentPlanId is long planId &&
                    planTypesById.TryGetValue(planId, out string? value)
                        ? value
                        : string.Empty;
                return new
                {
                    row.Bill,
                    row.Course,
                    row.Enrollment,
                    PlanTypeCode = planTypeCode,
                    IsInstallment = planTypeCode == "INSTALLMENT"
                };
            })
            .ToList();

        List<BillingStatementItem> existingItems = await dbContext.Set<BillingStatementItem>()
            .Where(x => x.BillingStatementId == statement.Id)
            .ToListAsync(cancellationToken);
        bool addedStatementItem = false;
        foreach (var row in bills)
        {
            BillingStatementItem? item = existingItems.SingleOrDefault(x => x.BillId == row.Bill.Id);
            if (item is null)
            {
                item = new BillingStatementItem(statement.Id, row.Bill.Id, row.Bill.OutstandingAmount, utcNow);
                await dbContext.Set<BillingStatementItem>().AddAsync(item, cancellationToken);
                existingItems.Add(item);
                addedStatementItem = true;
            }
            else
            {
                item.Refresh(row.Bill.OutstandingAmount, 0m);
            }
        }

        decimal total = bills.Sum(x => x.Bill.OutstandingAmount);
        statement.Refresh(total, 0m, utcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
        if (notificationMode == BillingStatementNotificationMode.SendMonthlyBill &&
            total > 0m &&
            (statementCreated || addedStatementItem))
        {
            await SendMonthlyBillEmailAsync(
                personId,
                monthStart,
                total,
                bills.Min(x => x.Bill.CurrentDueDate),
                cancellationToken);
        }

        long[] billIds = bills.Select(x => x.Bill.Id).ToArray();
        List<StatementBillLineProjection> billLines = await (
            from line in dbContext.Set<BillLine>().AsNoTracking()
            join component in dbContext.Set<FeeComponent>().AsNoTracking()
                on line.FeeComponentId equals component.Id
            where billIds.Contains(line.BillId)
            orderby line.BillId, line.Id
            select new StatementBillLineProjection(
                line.BillId,
                line.Id,
                line.FeeComponentId,
                line.CourseFeeId,
                component.ComponentCode,
                component.ComponentName,
                component.ComponentTypeCode,
                component.CalculationTypeCode,
                line.DescriptionSnapshot,
                line.Quantity,
                line.UnitAmount,
                line.GrossAmount,
                line.SubsidyAmount,
                line.NetAmount))
            .ToListAsync(cancellationToken);
        Dictionary<long, BillingStatementItemLineResponse[]> linesByBillId = billLines
            .GroupBy(line => line.BillId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(line => new BillingStatementItemLineResponse(
                        line.BillLineId,
                        line.FeeComponentId,
                        line.CourseFeeId,
                        line.ComponentCode,
                        line.ComponentName,
                        line.ComponentTypeCode,
                        line.CalculationTypeCode,
                        line.Description,
                        line.Quantity,
                        line.UnitAmount,
                        line.GrossAmount,
                        line.SubsidyAmount,
                        line.NetAmount))
                    .ToArray());

        return new BillingStatementResponse(
            statement.Id,
            year,
            month,
            statement.CurrencyCode,
            statement.TotalAmount,
            statement.PaidAmount,
            statement.OutstandingAmount,
            statement.StatementStatusCode,
            bills.Select(row =>
            {
                BillingStatementItem item = existingItems.Single(x => x.BillId == row.Bill.Id);
                return new BillingStatementItemResponse(
                    item.Id,
                    row.Bill.Id,
                    row.Enrollment.Id,
                    row.Course.Id,
                    row.Course.CourseCode,
                    row.Course.CourseName,
                    row.Bill.SequenceNumber,
                    row.Bill.OriginalDueDate,
                    row.Bill.CurrentDueDate,
                    row.Bill.DeferralCount,
                    row.Bill.GrossAmount,
                    row.Bill.SubsidyAmount,
                    row.Bill.NetPayableAmount,
                    row.Bill.OutstandingAmount,
                    row.Bill.BillStatusCode,
                    row.PlanTypeCode,
                    row.IsInstallment,
                    row.IsInstallment,
                    row.IsInstallment ? null : "Full payment bills cannot be deferred.",
                    linesByBillId.GetValueOrDefault(row.Bill.Id, []));
            }).ToArray());
    }

    private async Task<Dictionary<long, string>> LoadPlanTypesAsync(
        IReadOnlyCollection<long> coursePaymentPlanIds,
        CancellationToken cancellationToken)
    {
        Dictionary<long, string> result = new();
        foreach (long coursePaymentPlanId in coursePaymentPlanIds)
        {
            CourseBillingPlan? plan = await paymentPlans.FindPlanAsync(coursePaymentPlanId, cancellationToken);
            if (plan is not null)
            {
                result[coursePaymentPlanId] = plan.PlanTypeCode;
            }
        }
        return result;
    }

    private async Task SendMonthlyBillEmailAsync(
        long personId,
        DateOnly billingMonth,
        decimal totalAmountDue,
        DateOnly dueDate,
        CancellationToken cancellationToken)
    {
        if (!mailSwitch.IsEnabled)
        {
            logger.LogInformation(
                "Monthly bill email skipped because MailDelivery is disabled. PersonId={PersonId} BillingMonth={BillingMonth}",
                personId,
                billingMonth);
            return;
        }

        Person? person = await dbContext.Set<Person>()
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == personId, cancellationToken);

        string studentName = string.IsNullOrWhiteSpace(person?.OfficialFullName)
            ? "Student"
            : person.OfficialFullName.Trim();
        string monthName = billingMonth.ToString("MMMM", CultureInfo.InvariantCulture);
        string monthYear = billingMonth.ToString("MMMM yyyy", CultureInfo.InvariantCulture);
        string totalAmountDisplay = $"SGD {totalAmountDue:N2}";
        string dueDateDisplay = dueDate.ToString("dd MMM yyyy", CultureInfo.InvariantCulture);

        string subject = $"Your {monthName} Bill Is Ready";
        string plainTextBody = string.Join(Environment.NewLine, [
            branding.AppName,
            "Monthly bill notification",
            string.Empty,
            $"Hello {studentName}, your consolidated bill for {monthYear} is now ready.",
            string.Empty,
            $"Total Amount Due: {totalAmountDisplay}",
            $"Due Date: {dueDateDisplay}",
            string.Empty,
            "Please log in to review the breakdown and complete your payment.",
            $"View My Bill -> {branding.PaymentDashboardUrl}"
        ]);

        string htmlBody = BuildMonthlyBillHtmlBody(
            studentName,
            monthName,
            monthYear,
            totalAmountDisplay,
            dueDateDisplay,
            branding.AppName,
            branding.PaymentDashboardUrl);

        try
        {
            Result result = await mailQueue.EnqueueAsync(
                EmailNotificationJob.ForPerson(
                    "NOTI-01",
                    personId,
                    subject,
                    plainTextBody,
                    htmlBody,
                    "BillingStatement",
                    $"{billingMonth.Year:D4}-{billingMonth.Month:D2}"),
                cancellationToken);

            if (result.IsFailure)
            {
                logger.LogWarning(
                    "Monthly bill email enqueue failed. PersonId={PersonId} BillingMonth={BillingMonth} ErrorCode={ErrorCode}",
                    personId,
                    monthYear,
                    result.Error.Code);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(
                ex,
                "Monthly bill email notification threw an exception. PersonId={PersonId} BillingMonth={BillingMonth}",
                personId,
                monthYear);
        }
    }

    private static string BuildMonthlyBillHtmlBody(
        string studentName,
        string monthName,
        string monthYear,
        string totalAmountDisplay,
        string dueDateDisplay,
        string appName,
        string paymentDashboardUrl)
    {
        string encodedStudentName = WebUtility.HtmlEncode(studentName);
        string encodedMonthYear = WebUtility.HtmlEncode(monthYear);
        string encodedTotalAmount = WebUtility.HtmlEncode(totalAmountDisplay);
        string encodedDueDate = WebUtility.HtmlEncode(dueDateDisplay);

        StringBuilder builder = new();
        EmailTemplateBranding.AppendShellStart(builder);
        EmailTemplateBranding.AppendHeader(builder, $"Your {monthName} bill is ready", appName);
        builder.Append("<tr><td style=\"padding:30px;\">");
        builder.Append("<p style=\"font-size:16px;line-height:24px;margin:0 0 18px;color:#172033;\">Hello ")
            .Append(encodedStudentName)
            .Append(", your consolidated bill for <strong>")
            .Append(encodedMonthYear)
            .Append("</strong> is now ready.</p>");
        builder.Append("<table role=\"presentation\" width=\"100%\" border=\"0\" cellspacing=\"0\" cellpadding=\"0\" style=\"border-collapse:collapse;margin:0 0 24px;\">");
        AppendSummaryRow(builder, "Total Amount Due", encodedTotalAmount, EmailTemplateBranding.PrimarySoftColor, EmailTemplateBranding.PrimaryTextColor);
        AppendSummaryRow(builder, "Due Date", encodedDueDate, "#f8fafc", "#334155");
        builder.Append("</table>");
        builder.Append("<p style=\"font-size:15px;line-height:23px;margin:0 0 24px;color:#46566d;\">Please log in to review the breakdown and complete your payment.</p>");
        EmailTemplateBranding.AppendButton(builder, paymentDashboardUrl, "View My Bill");
        builder.Append("</td></tr>");
        EmailTemplateBranding.AppendFooter(builder, $"This message was sent by {appName}. If you have already paid, you can ignore this reminder.");
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

internal sealed record StatementBillLineProjection(
    long BillId,
    long BillLineId,
    long FeeComponentId,
    long? CourseFeeId,
    string ComponentCode,
    string ComponentName,
    string ComponentTypeCode,
    string CalculationTypeCode,
    string Description,
    decimal Quantity,
    decimal UnitAmount,
    decimal GrossAmount,
    decimal SubsidyAmount,
    decimal NetAmount);
