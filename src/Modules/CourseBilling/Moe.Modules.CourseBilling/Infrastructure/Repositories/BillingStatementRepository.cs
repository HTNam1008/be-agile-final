using Microsoft.EntityFrameworkCore;
using Moe.Modules.CourseBilling.Contracts.BillingStatements;
using Moe.Modules.CourseBilling.Domain.Billing;
using Moe.Modules.CourseBilling.Domain.Courses;
using Moe.Modules.CourseBilling.IGateway.Payments;
using Moe.Modules.CourseBilling.IGateway.Repositories;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.CourseBilling.Infrastructure.Repositories;

internal sealed class BillingStatementRepository(
    MoeDbContext dbContext,
    ICoursePaymentPlanGateway paymentPlans) : IBillingStatementRepository
{
    public async Task<BillingStatementResponse> GetOrCreateAsync(
        long personId,
        int year,
        int month,
        DateTime utcNow,
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
        if (statement is null)
        {
            statement = BillingStatement.Create(personId, year, month, utcNow);
            await dbContext.Set<BillingStatement>().AddAsync(statement, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
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
        foreach (var row in bills)
        {
            BillingStatementItem? item = existingItems.SingleOrDefault(x => x.BillId == row.Bill.Id);
            if (item is null)
            {
                item = new BillingStatementItem(statement.Id, row.Bill.Id, row.Bill.OutstandingAmount, utcNow);
                await dbContext.Set<BillingStatementItem>().AddAsync(item, cancellationToken);
                existingItems.Add(item);
            }
            else
            {
                item.Refresh(row.Bill.OutstandingAmount, 0m);
            }
        }

        decimal total = bills.Sum(x => x.Bill.OutstandingAmount);
        statement.Refresh(total, 0m, utcNow);
        await dbContext.SaveChangesAsync(cancellationToken);

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
