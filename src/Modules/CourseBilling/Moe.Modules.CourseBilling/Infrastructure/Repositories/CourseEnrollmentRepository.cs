using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moe.Modules.CourseBilling.Domain.Billing;
using Moe.Modules.CourseBilling.Domain.Courses;
using Moe.Modules.CourseBilling.IGateway.Fas;
using Moe.Modules.CourseBilling.IGateway.Payments;
using Moe.Modules.CourseBilling.IGateway.Repositories;
using Moe.Modules.IdentityPlatform.Domain.People;
using Moe.Modules.IdentityPlatform.IGateway.People;
using Moe.Modules.IdentityPlatform.Domain.Schooling;
using Moe.Modules.MailDelivery.IGateway;
using Moe.Modules.MailDelivery.Templates;
using Moe.SharedKernel.Results;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.CourseBilling.Infrastructure.Repositories;

internal sealed class CourseEnrollmentRepository(
    MoeDbContext dbContext,
    IEmailRecipientResolver recipientResolver,
    IEmailDeliveryGateway mailGateway,
    ILogger<CourseEnrollmentRepository> logger) : ICourseEnrollmentRepository
{
    private const string PaymentDashboardUrl = "http://localhost:5173/portal/payments";

    public async Task<long?> FindCourseOrganizationIdAsync(long courseId, CancellationToken cancellationToken)
    {
        return await dbContext.Set<Course>()
            .AsNoTracking()
            .Where(x => x.Id == courseId)
            .Select(x => (long?)x.OrganizationId)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public Task<Course?> FindCourseAsync(long courseId, CancellationToken cancellationToken)
    {
        return dbContext.Set<Course>()
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == courseId, cancellationToken);
    }

    public async Task<bool> PersonExistsAsync(long personId, CancellationToken cancellationToken)
    {
        int exists = await dbContext.Database.SqlQuery<int>($"""
            SELECT CAST(CASE WHEN EXISTS (
                SELECT 1
                FROM [person].[Person]
                WHERE [PersonId] = {personId}
            ) THEN 1 ELSE 0 END AS int) AS [Value]
            """)
            .SingleAsync(cancellationToken);

        return exists == 1;
    }

    public Task<long?> FindActiveStudentPersonIdAsync(
        string studentNumber,
        long organizationId,
        DateOnly onDate,
        CancellationToken cancellationToken)
    {
        string normalizedStudentNumber = studentNumber.Trim().ToUpperInvariant();

        return dbContext.Set<SchoolEnrollment>()
            .AsNoTracking()
            .Where(x => x.StudentNumber == normalizedStudentNumber
                && x.OrganizationId == organizationId
                && x.SchoolingStatusCode == "ACTIVE"
                && x.StartDate <= onDate
                && (x.EndDate == null || x.EndDate >= onDate))
            .OrderByDescending(x => x.StartDate)
            .ThenByDescending(x => x.Id)
            .Select(x => (long?)x.PersonId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> PersonHasActiveSchoolEnrollmentAsync(
        long personId,
        long organizationId,
        DateOnly onDate,
        CancellationToken cancellationToken)
        => await dbContext.Set<SchoolEnrollment>()
            .AsNoTracking()
            .AnyAsync(x => x.PersonId == personId
                && x.OrganizationId == organizationId
                && x.SchoolingStatusCode == "ACTIVE"
                && x.StartDate <= onDate
                && (x.EndDate == null || x.EndDate >= onDate),
                cancellationToken);

    public Task<bool> ExistsAsync(long personId, long courseId, CancellationToken cancellationToken)
    {
        return dbContext.Set<CourseEnrollment>()
            .AnyAsync(
                x => x.PersonId == personId
                    && x.CourseId == courseId
                    && x.EnrollmentStatusCode != CourseEnrollmentStatusCodes.Cancelled
                    && x.EnrollmentStatusCode != CourseEnrollmentStatusCodes.Refunded
                    && x.EnrollmentStatusCode != CourseEnrollmentStatusCodes.Exited,
                cancellationToken);
    }

    public async Task<IReadOnlyCollection<CourseFeeBillingLine>> ListActiveCourseFeesAsync(
        long courseId,
        CancellationToken cancellationToken)
    {
        return await (
                from courseFee in dbContext.Set<CourseFee>().AsNoTracking()
                join feeComponent in dbContext.Set<FeeComponent>().AsNoTracking()
                    on courseFee.FeeComponentId equals feeComponent.Id
                where courseFee.CourseId == courseId
                    && courseFee.IsActive
                    && feeComponent.IsActive
                orderby courseFee.SequenceNumber, courseFee.Id
                select new CourseFeeBillingLine(
                    courseFee.Id,
                    feeComponent.Id,
                    feeComponent.ComponentName,
                    feeComponent.CalculationTypeCode,
                    feeComponent.IsTaxComponent,
                    courseFee.FeeValue,
                    feeComponent.ComponentCode,
                    feeComponent.ComponentTypeCode))
            .ToArrayAsync(cancellationToken);
    }

    public async Task AddEnrollmentAsync(CourseEnrollment enrollment, CancellationToken cancellationToken)
    {
        await dbContext.Set<CourseEnrollment>().AddAsync(enrollment, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        if (enrollment.EnrollmentSourceCode == CourseEnrollmentSourceCodes.AdminAdd)
        {
            await SendAdminAddedEnrollmentEmailAsync(enrollment, cancellationToken);
        }
    }

    public async Task<CourseEnrollmentBillingResult> AddEnrollmentAndIssueBillsAsync(
        CourseEnrollment enrollment,
        string billNumberPrefix,
        DateTime issuedAtUtc,
        DateOnly firstDueDate,
        int installmentCount,
        int intervalMonths,
        IReadOnlyCollection<CourseFeeBillingLine> feeLines,
        IReadOnlyCollection<CourseFasSubsidy> fasSubsidies,
        CancellationToken cancellationToken)
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            if (IsInMemoryDatabase())
            {
                return await AddEnrollmentAndBillRowsAsync(
                    enrollment,
                    billNumberPrefix,
                    issuedAtUtc,
                    firstDueDate,
                    installmentCount,
                    intervalMonths,
                    feeLines,
                    fasSubsidies,
                    cancellationToken);
            }

            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            CourseEnrollmentBillingResult result = await AddEnrollmentAndBillRowsAsync(
                enrollment,
                billNumberPrefix,
                issuedAtUtc,
                firstDueDate,
                installmentCount,
                intervalMonths,
                feeLines,
                fasSubsidies,
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return result;
        });
    }

    public Task<CourseEnrollment?> FindEnrollmentAsync(
        long enrollmentId,
        long personId,
        CancellationToken cancellationToken)
        => dbContext.Set<CourseEnrollment>().SingleOrDefaultAsync(
            x => x.Id == enrollmentId && x.PersonId == personId,
            cancellationToken);

    public async Task<CourseEnrollmentBillingResult?> ChangePaymentPlanAndReissueBillsAsync(
        CourseEnrollment enrollment,
        long coursePaymentPlanId,
        bool installment,
        string billNumberPrefix,
        DateTime issuedAtUtc,
        DateOnly firstDueDate,
        int installmentCount,
        int intervalMonths,
        IReadOnlyCollection<CourseFeeBillingLine> feeLines,
        IReadOnlyCollection<CourseFasSubsidy> fasSubsidies,
        CancellationToken cancellationToken)
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = IsInMemoryDatabase()
                ? null
                : await dbContext.Database.BeginTransactionAsync(cancellationToken);

            Bill[] existingBills = await dbContext.Set<Bill>()
                .Where(x => x.CourseEnrollmentId == enrollment.Id)
                .ToArrayAsync(cancellationToken);
            if (existingBills.Any(x => x.PaidAmount > 0m || x.BillStatusCode == BillStatusCodes.Paid))
                return null;
            int activePaymentAttempts = IsInMemoryDatabase()
                ? 0
                : await dbContext.Database.SqlQuery<int>($"""
                    SELECT COUNT(*) AS [Value]
                    FROM [payment].[PaymentAllocation] allocation
                    INNER JOIN [payment].[Payment] payment
                        ON payment.[PaymentId] = allocation.[PaymentId]
                    INNER JOIN [billing].[Bill] bill
                        ON bill.[BillId] = allocation.[BillId]
                    WHERE bill.[CourseEnrollmentId] = {enrollment.Id}
                      AND payment.[PaymentStatusCode] NOT IN ('FAILED', 'CANCELLED', 'EXPIRED')
                    """).SingleAsync(cancellationToken);
            if (activePaymentAttempts > 0)
                return null;

            foreach (Bill bill in existingBills)
                bill.Cancel();

            enrollment.ChangePaymentPlan(coursePaymentPlanId, installment);
            await dbContext.SaveChangesAsync(cancellationToken);

            FasBillingCalculation calculation = FasBillingCalculator.Calculate(feeLines, fasSubsidies);
            IReadOnlyList<CourseFeeBillingAmount> totalAmounts = calculation.Amounts;
            List<GeneratedBillResult> generated = [];
            for (int sequence = 1; sequence <= installmentCount; sequence++)
            {
                IReadOnlyList<CourseFeeBillingAmount> installmentAmounts =
                    CourseFeeAmountCalculator.AllocateInstallment(totalAmounts, sequence, installmentCount);
                decimal installmentAmount = installmentAmounts.Sum(x => x.Amount);
                decimal installmentSubsidy = CourseFeeAmountCalculator.AllocateAmount(calculation.SubsidyAmount, sequence, installmentCount);
                DateOnly dueDate = firstDueDate.AddMonths((sequence - 1) * intervalMonths);
                Bill bill = Bill.IssueForCourseEnrollment(
                    enrollment.Id, $"{billNumberPrefix}-{sequence:D2}", issuedAtUtc,
                    dueDate, installmentAmount, installmentSubsidy, sequenceNumber: sequence).Value;
                await dbContext.Set<Bill>().AddAsync(bill, cancellationToken);
                await dbContext.SaveChangesAsync(cancellationToken);
                foreach (CourseFeeBillingAmount amount in installmentAmounts.Where(x => x.Amount > 0m))
                {
                    BillLine line = BillLine.FromCourseFee(
                        bill.Id,
                        amount.FeeComponentId,
                        amount.CourseFeeId,
                        $"{amount.FeeComponentName} installment {sequence} of {installmentCount}",
                        amount.Amount).Value;
                    await dbContext.Set<BillLine>().AddAsync(line, cancellationToken);
                }

                generated.Add(new GeneratedBillResult(bill, installmentAmounts.Count(x => x.Amount > 0m)));
            }
            await dbContext.SaveChangesAsync(cancellationToken);
            await FinalizeEnrollmentAccessForPaidGeneratedBillsAsync(enrollment, generated, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
            return new CourseEnrollmentBillingResult(enrollment, generated);
        });
    }

    public CourseEnrollmentBillingPreviewResult PreviewPaymentPlanBills(
        CourseBillingPlan plan,
        bool installment,
        DateOnly firstDueDate,
        IReadOnlyCollection<CourseFeeBillingLine> feeLines,
        IReadOnlyCollection<CourseFasSubsidy> fasSubsidies)
    {
        FasBillingCalculation calculation = FasBillingCalculator.Calculate(feeLines, fasSubsidies);
        Dictionary<long, CourseFeeBillingLine> feeByCourseFeeId = feeLines.ToDictionary(x => x.CourseFeeId);
        List<PreviewGeneratedBillResult> bills = [];
        for (int sequence = 1; sequence <= plan.InstallmentCount; sequence++)
        {
            IReadOnlyList<CourseFeeBillingAmount> amounts =
                CourseFeeAmountCalculator.AllocateInstallment(calculation.Amounts, sequence, plan.InstallmentCount);
            decimal grossAmount = amounts.Sum(x => x.Amount);
            decimal subsidyAmount = CourseFeeAmountCalculator.AllocateAmount(
                calculation.SubsidyAmount,
                sequence,
                plan.InstallmentCount);
            DateOnly dueDate = firstDueDate.AddMonths((sequence - 1) * plan.IntervalMonths);
            PreviewGeneratedBillLineResult[] lines = amounts
                .Where(x => x.Amount > 0m)
                .Select(amount =>
                {
                    CourseFeeBillingLine fee = feeByCourseFeeId[amount.CourseFeeId];
                    return new PreviewGeneratedBillLineResult(
                        amount.FeeComponentId,
                        amount.CourseFeeId,
                        fee.FeeComponentCode,
                        amount.FeeComponentName,
                        fee.FeeComponentTypeCode,
                        fee.CalculationTypeCode,
                        $"{amount.FeeComponentName} installment {sequence} of {plan.InstallmentCount}",
                        amount.Amount,
                        0m,
                        amount.Amount);
                })
                .ToArray();
            bills.Add(new PreviewGeneratedBillResult(
                sequence,
                dueDate,
                grossAmount,
                subsidyAmount,
                Math.Max(0m, grossAmount - subsidyAmount),
                installment,
                lines));
        }

        return new CourseEnrollmentBillingPreviewResult(
            bills.Sum(x => x.GrossAmount),
            bills.Sum(x => x.SubsidyAmount),
            bills.Sum(x => x.NetPayableAmount),
            bills);
    }

    private bool IsInMemoryDatabase()
    {
        return string.Equals(
            dbContext.Database.ProviderName,
            "Microsoft.EntityFrameworkCore.InMemory",
            StringComparison.Ordinal);
    }

    private async Task<CourseEnrollmentBillingResult> AddEnrollmentAndBillRowsAsync(
        CourseEnrollment enrollment,
        string billNumberPrefix,
        DateTime issuedAtUtc,
        DateOnly firstDueDate,
        int installmentCount,
        int intervalMonths,
        IReadOnlyCollection<CourseFeeBillingLine> feeLines,
        IReadOnlyCollection<CourseFasSubsidy> fasSubsidies,
        CancellationToken cancellationToken)
    {
        await dbContext.Set<CourseEnrollment>().AddAsync(enrollment, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        if (installmentCount <= 0 || intervalMonths < 0)
            throw new InvalidOperationException("The payment plan schedule is invalid.");

        FasBillingCalculation calculation = FasBillingCalculator.Calculate(feeLines, fasSubsidies);
        IReadOnlyList<CourseFeeBillingAmount> totalAmounts = calculation.Amounts;
        List<GeneratedBillResult> generated = [];

        for (int sequence = 1; sequence <= installmentCount; sequence++)
        {
            IReadOnlyList<CourseFeeBillingAmount> installmentAmounts =
                CourseFeeAmountCalculator.AllocateInstallment(totalAmounts, sequence, installmentCount);
            decimal installmentAmount = installmentAmounts.Sum(x => x.Amount);
            decimal installmentSubsidy = CourseFeeAmountCalculator.AllocateAmount(calculation.SubsidyAmount, sequence, installmentCount);
            DateOnly dueDate = firstDueDate.AddMonths((sequence - 1) * intervalMonths);
            Result<Bill> billResult = Bill.IssueForCourseEnrollment(
                enrollment.Id,
                $"{billNumberPrefix}-{sequence:D2}",
                issuedAtUtc,
                dueDate,
                installmentAmount,
                installmentSubsidy,
                sequenceNumber: sequence);
            if (billResult.IsFailure)
                throw new InvalidOperationException(billResult.Error.Message);

            await dbContext.Set<Bill>().AddAsync(billResult.Value, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            int billLineCount = 0;
            foreach (CourseFeeBillingAmount amount in installmentAmounts.Where(x => x.Amount > 0m))
            {
                Result<BillLine> lineResult = BillLine.FromCourseFee(
                    billResult.Value.Id,
                    amount.FeeComponentId,
                    amount.CourseFeeId,
                    $"{amount.FeeComponentName} installment {sequence} of {installmentCount}",
                    amount.Amount);
                if (lineResult.IsFailure)
                    throw new InvalidOperationException(lineResult.Error.Message);

                await dbContext.Set<BillLine>().AddAsync(lineResult.Value, cancellationToken);
                billLineCount++;
            }

            generated.Add(new GeneratedBillResult(billResult.Value, billLineCount));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await FinalizeEnrollmentAccessForPaidGeneratedBillsAsync(enrollment, generated, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new CourseEnrollmentBillingResult(enrollment, generated);
    }

    private async Task FinalizeEnrollmentAccessForPaidGeneratedBillsAsync(
        CourseEnrollment enrollment,
        IReadOnlyCollection<GeneratedBillResult> generated,
        CancellationToken cancellationToken)
    {
        if (!generated.Any(x => x.Bill.BillStatusCode == BillStatusCodes.Paid))
        {
            return;
        }

        bool allBillsPaid = await dbContext.Set<Bill>()
            .Where(candidate => candidate.CourseEnrollmentId == enrollment.Id)
            .AllAsync(candidate =>
                candidate.BillStatusCode == BillStatusCodes.Paid ||
                candidate.BillStatusCode == BillStatusCodes.Cancelled,
                cancellationToken);
        enrollment.GrantPaidAccess(allBillsPaid);
    }

    private async Task SendAdminAddedEnrollmentEmailAsync(
        CourseEnrollment enrollment,
        CancellationToken cancellationToken)
    {
        EmailRecipient? recipient;
        try
        {
            recipient = await recipientResolver.ResolveForPersonAsync(enrollment.PersonId, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Admin-added course enrollment recipient resolution failed. PersonId={PersonId} CourseEnrollmentId={CourseEnrollmentId}", enrollment.PersonId, enrollment.Id);
            return;
        }

        if (recipient is null)
        {
            logger.LogWarning("Admin-added course enrollment email skipped because no valid recipient was found. PersonId={PersonId} CourseEnrollmentId={CourseEnrollmentId}", enrollment.PersonId, enrollment.Id);
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
        const string feePayableDisplay = "To be confirmed after payment plan selection";
        const string dueDateDisplay = "To be confirmed after payment plan selection";

        string subject = $"You've Been Enrolled in {courseName}";
        string plainTextBody = string.Join(Environment.NewLine, [
            "MOE SEEDS",
            "Course enrollment notification",
            string.Empty,
            $"Hello {studentName}, you have been enrolled in {courseName} by your school administrator.",
            string.Empty,
            $"Fee Payable: {feePayableDisplay}",
            $"Payment Due Date: {dueDateDisplay}",
            string.Empty,
            "Please log in to complete your payment and secure your spot in this course.",
            $"Go to Payment Dashboard -> {PaymentDashboardUrl}"
        ]);

        string htmlBody = BuildAdminAddedEnrollmentHtmlBody(
            studentName,
            courseName,
            feePayableDisplay,
            dueDateDisplay);

        try
        {
            Result result = await mailGateway.SendAsync(
                new EmailDeliveryMessage(recipient.EmailAddress, subject, plainTextBody, htmlBody),
                cancellationToken);

            if (result.IsFailure)
            {
                logger.LogWarning(
                    "Admin-added course enrollment email failed. CourseEnrollmentId={CourseEnrollmentId} ErrorCode={ErrorCode}",
                    enrollment.Id,
                    result.Error.Code);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(
                ex,
                "Admin-added course enrollment email threw an exception. CourseEnrollmentId={CourseEnrollmentId}",
                enrollment.Id);
        }
    }

    private static string BuildAdminAddedEnrollmentHtmlBody(
        string studentName,
        string courseName,
        string feePayableDisplay,
        string dueDateDisplay)
    {
        string encodedStudentName = WebUtility.HtmlEncode(studentName);
        string encodedCourseName = WebUtility.HtmlEncode(courseName);
        string encodedFeePayable = WebUtility.HtmlEncode(feePayableDisplay);
        string encodedDueDate = WebUtility.HtmlEncode(dueDateDisplay);

        StringBuilder builder = new();
        builder.Append("<!doctype html><html><head><meta http-equiv=\"Content-Type\" content=\"text/html; charset=utf-8\"></head>");
        builder.Append("<body bgcolor=\"#eef4fb\" style=\"margin:0;padding:0;background-color:#eef4fb;font-family:Arial,Helvetica,sans-serif;color:#172033;\">");
        builder.Append("<table role=\"presentation\" width=\"100%\" border=\"0\" cellspacing=\"0\" cellpadding=\"0\" bgcolor=\"#eef4fb\" style=\"background-color:#eef4fb;\">");
        builder.Append("<tr><td align=\"center\" style=\"padding:28px 12px;\">");
        builder.Append("<table role=\"presentation\" width=\"640\" border=\"0\" cellspacing=\"0\" cellpadding=\"0\" bgcolor=\"#ffffff\" style=\"width:640px;max-width:100%;background-color:#ffffff;border:1px solid #dce3ee;\">");
        EmailTemplateBranding.AppendHeader(builder, $"You've been enrolled in {courseName}");
        builder.Append("<tr><td style=\"padding:30px;\">");
        builder.Append("<p style=\"font-size:16px;line-height:24px;margin:0 0 18px;color:#172033;\">Hello ")
            .Append(encodedStudentName)
            .Append(", you have been enrolled in <strong>")
            .Append(encodedCourseName)
            .Append("</strong> by your school administrator.</p>");
        builder.Append("<table role=\"presentation\" width=\"100%\" border=\"0\" cellspacing=\"0\" cellpadding=\"0\" style=\"border-collapse:collapse;margin:0 0 24px;\">");
        AppendSummaryRow(builder, "Course", encodedCourseName, EmailTemplateBranding.PrimarySoftColor, EmailTemplateBranding.PrimaryTextColor);
        AppendSummaryRow(builder, "Fee Payable", encodedFeePayable, "#f8fafc", "#334155");
        AppendSummaryRow(builder, "Payment Due Date", encodedDueDate, "#f8fafc", "#334155");
        builder.Append("</table>");
        builder.Append("<p style=\"font-size:15px;line-height:23px;margin:0 0 24px;color:#46566d;\">Please log in to complete your payment and secure your spot in this course.</p>");
        EmailTemplateBranding.AppendButton(builder, PaymentDashboardUrl, "Go to Payment Dashboard");
        builder.Append("</td></tr>");
        builder.Append("<tr><td bgcolor=\"#f8fafc\" style=\"background-color:#f8fafc;padding:18px 30px;color:#64748b;font-size:12px;line-height:18px;\">This message was sent by MOE SEEDS after your school administrator added you to a course.</td></tr>");
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
        builder.Append("<div style=\"font-size:18px;line-height:26px;color:")
            .Append(valueColor)
            .Append(";font-weight:bold;padding-top:4px;\">")
            .Append(value)
            .Append("</div>");
        builder.Append("</td></tr>");
    }

}
