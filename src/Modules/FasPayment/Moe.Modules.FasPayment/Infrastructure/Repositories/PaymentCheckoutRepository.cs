using Microsoft.EntityFrameworkCore;
using Moe.Modules.FasPayment.Domain.Payments;
using Moe.Modules.FasPayment.IGateway.Payments;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.FasPayment.Infrastructure.Repositories;

internal sealed class PaymentCheckoutRepository(MoeDbContext dbContext) : IPaymentCheckoutRepository
{
    public async Task<int> GetNextPlanVersionAsync(long courseId, CancellationToken cancellationToken)
        => (await dbContext.Set<CoursePaymentPlan>()
            .Where(plan => plan.CourseId == courseId)
            .MaxAsync(plan => (int?)plan.Version, cancellationToken) ?? 0) + 1;

    public async Task AddPlanAsync(CoursePaymentPlan plan, CancellationToken cancellationToken)
    {
        CoursePaymentPlan[] previousVersions = await dbContext.Set<CoursePaymentPlan>()
            .Where(candidate => candidate.CourseId == plan.CourseId
                && candidate.PlanTypeCode == plan.PlanTypeCode
                && candidate.InstallmentCount == plan.InstallmentCount
                && candidate.IsActive)
            .ToArrayAsync(cancellationToken);
        foreach (CoursePaymentPlan previous in previousVersions)
            previous.Deactivate(plan.CreatedAtUtc);
        await dbContext.Set<CoursePaymentPlan>().AddAsync(plan, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<CoursePaymentPlan?> FindPlanAsync(long planId, CancellationToken cancellationToken)
        => dbContext.Set<CoursePaymentPlan>().SingleOrDefaultAsync(plan => plan.Id == planId, cancellationToken);

    public async Task<IReadOnlyCollection<CoursePaymentPlan>> ListActivePlansAsync(
        long courseId,
        CancellationToken cancellationToken)
        => await dbContext.Set<CoursePaymentPlan>()
            .AsNoTracking()
            .Where(plan => plan.CourseId == courseId && plan.IsActive)
            .OrderBy(plan => plan.InstallmentCount)
            .ThenByDescending(plan => plan.Version)
            .ToArrayAsync(cancellationToken);

    public Task<PaymentCheckoutSession?> FindOpenCheckoutAsync(
        long billId,
        long personId,
        CancellationToken cancellationToken)
        => dbContext.Set<PaymentCheckoutSession>()
            .SingleOrDefaultAsync(checkout =>
                checkout.BillId == billId &&
                checkout.PersonId == personId &&
                checkout.CheckoutStatusCode != CheckoutStatusCodes.PaidInFull &&
                checkout.CheckoutStatusCode != CheckoutStatusCodes.Cancelled,
                cancellationToken);

    public Task<PaymentCheckoutSession?> FindCheckoutAsync(
        long checkoutId,
        long personId,
        CancellationToken cancellationToken)
        => dbContext.Set<PaymentCheckoutSession>()
            .AsNoTracking()
            .SingleOrDefaultAsync(checkout => checkout.Id == checkoutId && checkout.PersonId == personId, cancellationToken);

    public Task<PaymentCheckoutSession?> FindCheckoutAsync(long checkoutId, CancellationToken cancellationToken)
        => dbContext.Set<PaymentCheckoutSession>().SingleOrDefaultAsync(checkout => checkout.Id == checkoutId, cancellationToken);

    public Task<PaymentCheckoutSession?> FindCheckoutByProviderSessionAsync(
        string providerSessionId,
        CancellationToken cancellationToken)
        => dbContext.Set<PaymentCheckoutSession>()
            .SingleOrDefaultAsync(checkout => checkout.ProviderCheckoutSessionId == providerSessionId, cancellationToken);

    public Task<PaymentCheckoutSession?> FindCheckoutBySubscriptionAsync(
        string providerSubscriptionId,
        CancellationToken cancellationToken)
        => dbContext.Set<PaymentCheckoutSession>()
            .SingleOrDefaultAsync(checkout => checkout.ProviderSubscriptionId == providerSubscriptionId, cancellationToken);

    public async Task AddCheckoutAsync(PaymentCheckoutSession checkout, CancellationToken cancellationToken)
    {
        await dbContext.Set<PaymentCheckoutSession>().AddAsync(checkout, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task AddStatementPaymentAsync(
        Payment payment,
        IReadOnlyCollection<PaymentPart> parts,
        IReadOnlyCollection<PaymentAllocation> allocations,
        CancellationToken cancellationToken)
    {
        await dbContext.Set<Payment>().AddAsync(payment, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        foreach (PaymentPart part in parts)
            part.AttachToPayment(payment.Id);
        foreach (PaymentAllocation allocation in allocations)
            allocation.AttachToPayment(payment.Id);
        await dbContext.Set<PaymentPart>().AddRangeAsync(parts, cancellationToken);
        await dbContext.Set<PaymentAllocation>().AddRangeAsync(allocations, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<PaymentPart>> ListPaymentPartsAsync(long paymentId, CancellationToken cancellationToken)
        => await dbContext.Set<PaymentPart>().Where(x => x.PaymentId == paymentId).OrderBy(x => x.SequenceNumber).ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyCollection<PaymentAllocation>> ListPaymentAllocationsAsync(long paymentId, CancellationToken cancellationToken)
        => await dbContext.Set<PaymentAllocation>().Where(x => x.PaymentId == paymentId).ToArrayAsync(cancellationToken);

    public Task<bool> PaymentReferenceExistsAsync(string providerReference, CancellationToken cancellationToken)
        => dbContext.Set<Payment>().AnyAsync(payment =>
            payment.ProviderPaymentIntentId == providerReference ||
            payment.ProviderInvoiceId == providerReference,
            cancellationToken);

    public Task AddPaymentAsync(Payment payment, CancellationToken cancellationToken)
        => dbContext.Set<Payment>().AddAsync(payment, cancellationToken).AsTask();

    public Task<Payment?> FindPaymentAsync(long paymentId, CancellationToken cancellationToken)
        => dbContext.Set<Payment>().SingleOrDefaultAsync(payment => payment.Id == paymentId, cancellationToken);

    public Task<Payment?> FindActiveStatementPaymentAsync(
        long billingStatementId,
        long personId,
        CancellationToken cancellationToken)
        => dbContext.Set<Payment>()
            .Where(payment =>
                payment.BillingStatementId == billingStatementId &&
                payment.PayerPersonId == personId &&
                (payment.PaymentStatusCode == PaymentStatusCodes.Initiated ||
                 payment.PaymentStatusCode == PaymentStatusCodes.PendingOnlinePayment))
            .OrderByDescending(payment => payment.InitiatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyCollection<Payment>> ListActiveStatementPaymentsAsync(
        long billingStatementId,
        long personId,
        long excludingPaymentId,
        CancellationToken cancellationToken)
        => await dbContext.Set<Payment>()
            .Where(payment =>
                payment.Id != excludingPaymentId &&
                payment.BillingStatementId == billingStatementId &&
                payment.PayerPersonId == personId &&
                (payment.PaymentStatusCode == PaymentStatusCodes.Initiated ||
                 payment.PaymentStatusCode == PaymentStatusCodes.PendingOnlinePayment))
            .OrderByDescending(payment => payment.InitiatedAtUtc)
            .ToArrayAsync(cancellationToken);

    public Task<PaymentCheckoutSession?> FindCheckoutByPaymentAsync(
        long paymentId,
        CancellationToken cancellationToken)
        => dbContext.Set<PaymentCheckoutSession>()
            .SingleOrDefaultAsync(checkout => checkout.PaymentId == paymentId, cancellationToken);

    public Task<Payment?> FindPaymentByChargeAsync(string providerChargeId, CancellationToken cancellationToken)
        => dbContext.Set<Payment>().SingleOrDefaultAsync(payment => payment.ProviderChargeId == providerChargeId, cancellationToken);

    public async Task<IReadOnlyCollection<Payment>> ListPaymentsAsync(CancellationToken cancellationToken)
        => await dbContext.Set<Payment>().AsNoTracking().OrderByDescending(payment => payment.InitiatedAtUtc).Take(200).ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyCollection<Payment>> ListPaymentsForPersonAsync(
        long personId,
        CancellationToken cancellationToken)
        => await dbContext.Set<Payment>()
            .AsNoTracking()
            .Where(payment => payment.PayerPersonId == personId)
            .OrderByDescending(payment => payment.InitiatedAtUtc)
            .Take(100)
            .ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyCollection<PaymentPart>> ListPaymentPartsForPaymentsAsync(
        IReadOnlyCollection<long> paymentIds,
        CancellationToken cancellationToken)
    {
        if (paymentIds.Count == 0)
        {
            return Array.Empty<PaymentPart>();
        }

        return await dbContext.Set<PaymentPart>()
            .AsNoTracking()
            .Where(part => paymentIds.Contains(part.PaymentId))
            .OrderBy(part => part.PaymentId)
            .ThenBy(part => part.SequenceNumber)
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<PaymentRefund>> ListPaymentRefundsForPaymentsAsync(
        IReadOnlyCollection<long> paymentIds,
        CancellationToken cancellationToken)
    {
        if (paymentIds.Count == 0)
        {
            return Array.Empty<PaymentRefund>();
        }

        return await dbContext.Set<PaymentRefund>()
            .AsNoTracking()
            .Where(refund => paymentIds.Contains(refund.PaymentId))
            .OrderByDescending(refund => refund.RequestedAtUtc)
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<EnrollmentRefundPart>> ListEnrollmentRefundPartsForPaymentsAsync(
        IReadOnlyCollection<long> paymentIds,
        CancellationToken cancellationToken)
    {
        if (paymentIds.Count == 0)
        {
            return Array.Empty<EnrollmentRefundPart>();
        }

        return await dbContext.Set<EnrollmentRefundPart>()
            .AsNoTracking()
            .Where(part => part.PaymentId != null && paymentIds.Contains(part.PaymentId.Value))
            .OrderByDescending(part => part.CreatedAtUtc)
            .ToArrayAsync(cancellationToken);
    }

    public Task<decimal> GetSucceededRefundAmountAsync(long paymentId, CancellationToken cancellationToken)
        => dbContext.Set<PaymentRefund>()
            .Where(refund => refund.PaymentId == paymentId && refund.RefundStatusCode != RefundStatusCodes.Failed)
            .SumAsync(refund => refund.Amount, cancellationToken);

    public async Task AddRefundAsync(PaymentRefund refund, CancellationToken cancellationToken)
    {
        await dbContext.Set<PaymentRefund>().AddAsync(refund, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<PaymentRefund>> ListRefundsAsync(long paymentId, CancellationToken cancellationToken)
        => await dbContext.Set<PaymentRefund>().Where(refund => refund.PaymentId == paymentId).ToArrayAsync(cancellationToken);

    public Task<EnrollmentRefund?> FindEnrollmentRefundByIdempotencyKeyAsync(
        string idempotencyKey,
        CancellationToken cancellationToken)
        => dbContext.Set<EnrollmentRefund>()
            .SingleOrDefaultAsync(
                refund => refund.IdempotencyKey == idempotencyKey,
                cancellationToken);

    public async Task AddEnrollmentRefundAsync(
        EnrollmentRefund refund,
        CancellationToken cancellationToken)
    {
        await dbContext.Set<EnrollmentRefund>().AddAsync(refund, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task AddEnrollmentRefundPartAsync(
        EnrollmentRefundPart refundPart,
        CancellationToken cancellationToken)
    {
        await dbContext.Set<EnrollmentRefundPart>().AddAsync(refundPart, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<EnrollmentRefundPart>> ListEnrollmentRefundPartsAsync(
        long enrollmentRefundId,
        CancellationToken cancellationToken)
        => await dbContext.Set<EnrollmentRefundPart>()
            .Where(part => part.EnrollmentRefundId == enrollmentRefundId)
            .OrderBy(part => part.Id)
            .ToArrayAsync(cancellationToken);

    public Task<EnrollmentRefundPart?> FindEnrollmentRefundPartByIdempotencyKeyAsync(
        string idempotencyKey,
        CancellationToken cancellationToken)
        => dbContext.Set<EnrollmentRefundPart>()
            .SingleOrDefaultAsync(
                part => part.IdempotencyKey == idempotencyKey,
                cancellationToken);

    public async Task<IReadOnlyCollection<ProcessedPaymentWebhookEvent>> ListWebhookEventsAsync(CancellationToken cancellationToken)
        => await dbContext.Set<ProcessedPaymentWebhookEvent>().AsNoTracking().OrderByDescending(item => item.ReceivedAtUtc).Take(200).ToArrayAsync(cancellationToken);

    public Task<bool> WebhookEventExistsAsync(string providerEventId, CancellationToken cancellationToken)
        => dbContext.Set<ProcessedPaymentWebhookEvent>()
            .AnyAsync(webhookEvent => webhookEvent.ProviderEventId == providerEventId, cancellationToken);

    public Task AddWebhookEventAsync(
        ProcessedPaymentWebhookEvent webhookEvent,
        CancellationToken cancellationToken)
        => dbContext.Set<ProcessedPaymentWebhookEvent>().AddAsync(webhookEvent, cancellationToken).AsTask();

    public async Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken)
    {
        if (string.Equals(
            dbContext.Database.ProviderName,
            "Microsoft.EntityFrameworkCore.InMemory",
            StringComparison.Ordinal))
        {
            await operation(cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        var strategy = dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            await operation(cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        });
    }
}
