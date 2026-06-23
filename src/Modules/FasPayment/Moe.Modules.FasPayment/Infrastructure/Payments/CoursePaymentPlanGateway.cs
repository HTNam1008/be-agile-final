using Microsoft.EntityFrameworkCore;
using Moe.Modules.CourseBilling.IGateway.Payments;
using Moe.Modules.FasPayment.Domain.Payments;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.FasPayment.Infrastructure.Payments;

internal sealed class CoursePaymentPlanGateway(MoeDbContext dbContext) : ICoursePaymentPlanGateway
{
    public Task<CourseBillingPlan?> FindPlanAsync(
        long coursePaymentPlanId,
        CancellationToken cancellationToken)
        => dbContext.Set<CoursePaymentPlan>()
            .AsNoTracking()
            .Where(plan => plan.Id == coursePaymentPlanId)
            .Select(plan => new CourseBillingPlan(
                plan.Id,
                plan.CourseId,
                plan.PlanTypeCode,
                plan.InstallmentCount,
                plan.IntervalMonths,
                plan.IsActive))
            .SingleOrDefaultAsync(cancellationToken);
}
