using Microsoft.Extensions.Logging;
using Moe.Application.Abstractions.Audit;
using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Messaging;
using Moe.Modules.CourseBilling.IGateway.Fas;
using Moe.Modules.CourseBilling.IGateway.Payments;
using Moe.Modules.EducationAccountTopUp.IGateway.Accounts;
using Moe.Modules.FasPayment.Application.Audit;
using Moe.Modules.FasPayment.Application.Notifications;
using Moe.Modules.FasPayment.Domain.Payments;
using Moe.Modules.FasPayment.IGateway.Payments;
using Moe.SharedKernel.Results;

namespace Moe.Modules.FasPayment.Application.Webhooks;

internal sealed record ProcessStripeWebhookCommand(string Payload, string SignatureHeader) : ICommand;

internal sealed class ProcessStripeWebhookHandler(
    IPaymentCheckoutRepository payments,
    IStripePaymentGateway stripe,
    ICoursePaymentGateway courses,
    IFasCourseSubsidyGateway fasSubsidies,
    IEducationAccountPaymentGateway accounts,
    FasInAppNotificationService fasNotifications,
    IClock clock,
    IPaymentPersistenceTracker persistenceTracker,
    IStripeWebhookCoordinator coordinator,
    IPaymentSchoolAuditRecorder paymentAudit,
    PaymentNotificationEmailService paymentNotifications,
    ILogger<ProcessStripeWebhookHandler> logger) : ICommandHandler<ProcessStripeWebhookCommand>
{
    public async Task<Result> Handle(
        ProcessStripeWebhookCommand command,
        CancellationToken cancellationToken)
    {
        ParsedPaymentWebhook webhook;
        try
        {
            webhook = stripe.ParseWebhook(command.Payload, command.SignatureHeader);
        }
        catch (InvalidPaymentWebhookException)
        {
            return Result.Failure(PaymentDomainErrors.InvalidWebhook);
        }
        return await coordinator.ExecuteAsync(
            webhook.CheckoutId,
            () => ProcessWithRetryAsync(webhook, cancellationToken),
            cancellationToken);
    }

    private async Task<Result> ProcessWithRetryAsync(
        ParsedPaymentWebhook webhook,
        CancellationToken cancellationToken)
    {
        const int maxAttempts = 3;
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                if (await payments.WebhookEventExistsAsync(webhook.ProviderEventId, cancellationToken))
                    return Result.Success();

                ProcessedPaymentWebhookEvent tracked = new(
                    webhook.ProviderEventId,
                    webhook.EventType,
                    clock.UtcNow.UtcDateTime);
                await payments.ExecuteInTransactionAsync(async transactionToken =>
                {
                    await payments.AddWebhookEventAsync(tracked, transactionToken);
                    if (webhook.Kind != PaymentWebhookKind.Ignored)
                        await ApplyAsync(webhook, transactionToken);
                    tracked.MarkProcessed(clock.UtcNow.UtcDateTime);
                }, cancellationToken);
                return Result.Success();
            }
            catch (Exception exception) when (persistenceTracker.IsRetryablePersistenceConflict(exception) && attempt < maxAttempts)
            {
                LogConcurrency(exception, webhook, attempt);
                persistenceTracker.ClearTrackedChanges();
                await Task.Delay(TimeSpan.FromMilliseconds(25 * attempt), cancellationToken);
            }
            catch (Exception exception) when (persistenceTracker.IsRetryablePersistenceConflict(exception))
            {
                LogConcurrency(exception, webhook, attempt);
                throw;
            }
        }

        return Result.Success();
    }

    private void LogConcurrency(
        Exception exception,
        ParsedPaymentWebhook webhook,
        int attempt)
    {
        string entities = persistenceTracker.DescribeConflict(exception);
        logger.LogWarning(
            exception,
            "Stripe webhook concurrency conflict. EventId={EventId}, EventType={EventType}, CheckoutId={CheckoutId}, Attempt={Attempt}, Entities={Entities}",
            webhook.ProviderEventId,
            webhook.EventType,
            webhook.CheckoutId,
            attempt,
            entities);
    }

    private async Task ApplyAsync(ParsedPaymentWebhook webhook, CancellationToken cancellationToken)
    {
        if (webhook.Kind == PaymentWebhookKind.ChargeRefunded)
        {
            await ApplyRefundAsync(webhook, cancellationToken);
            return;
        }
        PaymentCheckoutSession checkout = await payments.FindCheckoutAsync(webhook.CheckoutId, cancellationToken)
            ?? throw new InvalidOperationException("Payment checkout was not found.");

        switch (webhook.Kind)
        {
            case PaymentWebhookKind.CheckoutCompleted:
                await AttachCheckoutReferencesAsync(checkout, webhook, cancellationToken);
                break;
            case PaymentWebhookKind.PaymentSucceeded:
            case PaymentWebhookKind.InvoicePaid:
            {
                if (checkout is StatementPaymentCheckoutSession statementCheckout)
                    await RecordStatementSuccessAsync(statementCheckout, webhook, cancellationToken);
                else if (checkout is BillPaymentCheckoutSession billCheckout)
                    await RecordSuccessAsync(billCheckout, webhook, cancellationToken);
                break;
            }
            case PaymentWebhookKind.PaymentFailed:
            case PaymentWebhookKind.InvoicePaymentFailed:
            {
                if (checkout is StatementPaymentCheckoutSession statementCheckout)
                    await RecordStatementFailureAsync(statementCheckout, webhook.CreatedAtUtc, cancellationToken);
                else if (checkout is BillPaymentCheckoutSession billCheckout && billCheckout.RecordPaymentFailure(webhook.CreatedAtUtc))
                    await courses.ApplyPaymentFailureAsync(
                        billCheckout.BillId,
                        "The payment provider reported a failed payment. Please try again.",
                        cancellationToken);
                break;
            }
            case PaymentWebhookKind.CheckoutExpired:
            {
                if (checkout is StatementPaymentCheckoutSession statementCheckout)
                    await RecordStatementExpirationAsync(statementCheckout, webhook.CreatedAtUtc, cancellationToken);
                else if (checkout is BillPaymentCheckoutSession billCheckout && billCheckout.ExpireBeforePayment(webhook.CreatedAtUtc))
                    await courses.ApplyPaymentFailureAsync(
                        billCheckout.BillId,
                        "The payment session expired before completion. Please try again.",
                        cancellationToken);
                break;
            }
            case PaymentWebhookKind.SubscriptionDeleted:
                if (checkout.CheckoutStatusCode != CheckoutStatusCodes.PaidInFull)
                    checkout.RecordPaymentFailure(webhook.CreatedAtUtc);
                break;
            case PaymentWebhookKind.Ignored:
            default:
                break;
        }
    }

    private async Task RecordStatementSuccessAsync(
        StatementPaymentCheckoutSession checkout,
        ParsedPaymentWebhook webhook,
        CancellationToken cancellationToken)
    {
        Payment payment = await payments.FindPaymentAsync(checkout.PaymentId!.Value, cancellationToken)
            ?? throw new InvalidOperationException("Statement payment was not found.");
        if (payment.PaymentStatusCode == PaymentStatusCodes.Successful) return;
        if (payment.PaymentStatusCode is PaymentStatusCodes.Cancelled or PaymentStatusCodes.Expired or PaymentStatusCodes.Failed)
            return;
        string beforeStatus = payment.PaymentStatusCode;
        if (webhook.AmountMinor != decimal.ToInt64(payment.OnlinePaymentAmount * 100m))
            throw new InvalidOperationException("Stripe amount does not match the pending statement payment.");

        IReadOnlyCollection<PaymentPart> parts = await payments.ListPaymentPartsAsync(payment.Id, cancellationToken);
        PaymentPart? educationPart = parts.SingleOrDefault(x => x.PaymentMethodCode == PaymentMethodCodes.EducationAccount);
        PaymentPart onlinePart = parts.Single(x => x.PaymentMethodCode == PaymentMethodCodes.OnlinePayment);
        if (educationPart?.AccountHoldId is long holdId)
        {
            long accountTransactionId;
            try
            {
                accountTransactionId = await accounts.CaptureAsync(holdId, null, cancellationToken);
            }
            catch (EducationAccountPaymentUnavailableException exception)
            {
                logger.LogWarning(
                    exception,
                    "Statement payment {PaymentId} could not capture Education Account hold {AccountHoldId}.",
                    payment.Id,
                    holdId);
                educationPart.MarkCompleted(PaymentPartStatusCodes.Failed, webhook.CreatedAtUtc);
                onlinePart.MarkCompleted(PaymentPartStatusCodes.Successful, webhook.CreatedAtUtc);
                payment.MarkFailed(webhook.CreatedAtUtc);
                checkout.RecordPaymentFailure(webhook.CreatedAtUtc);
                return;
            }

            educationPart.MarkCompleted(PaymentPartStatusCodes.Captured, webhook.CreatedAtUtc, accountTransactionId);
        }
        onlinePart.MarkCompleted(PaymentPartStatusCodes.Successful, webhook.CreatedAtUtc);
        payment.AttachProviderReferences(
            webhook.ProviderPaymentIntentId,
            webhook.ProviderInvoiceId,
            webhook.ProviderChargeId,
            webhook.CreatedAtUtc);
        await AttachProviderEvidenceAsync(payment, checkout, webhook, cancellationToken);
        IReadOnlyCollection<PaymentAllocation> allocations = await payments.ListPaymentAllocationsAsync(payment.Id, cancellationToken);
        foreach (PaymentAllocation allocation in allocations) allocation.MarkApplied();
        await courses.ApplyStatementPaymentAsync(
            payment.BillingStatementId!.Value,
            allocations.Select(x => new BillPaymentAllocation(x.BillId, x.AllocatedAmount)).ToArray(),
            webhook.CreatedAtUtc,
            cancellationToken);
        await fasSubsidies.RedeemPendingRedemptionsForBillsAsync(
            allocations.Select(x => x.BillId).ToArray(),
            webhook.CreatedAtUtc,
            cancellationToken);
        payment.MarkSuccessful(webhook.CreatedAtUtc);
        checkout.RecordSuccessfulPayment(webhook.CreatedAtUtc);
        await paymentAudit.RecordPaymentAsync(
            AuditActionCodes.PaymentCompleted,
            payment,
            allocations.Select(x => x.BillId).ToArray(),
            "Payment completed",
            beforeStatus,
            payment.PaymentStatusCode,
            "STRIPE_WEBHOOK",
            cancellationToken);
        await fasNotifications.SendPaymentSucceededAsync(payment.Id, cancellationToken);
        await CancelCompetingStatementPaymentsAsync(payment, webhook.CreatedAtUtc, cancellationToken);
        await paymentNotifications.SendPaymentSucceededAsync(payment, webhook.CreatedAtUtc, cancellationToken);
    }

    private async Task CancelCompetingStatementPaymentsAsync(
        Payment successfulPayment,
        DateTime completedAtUtc,
        CancellationToken cancellationToken)
    {
        IReadOnlyCollection<Payment> competingPayments =
            await payments.ListActiveStatementPaymentsAsync(
                successfulPayment.BillingStatementId!.Value,
                successfulPayment.PayerPersonId,
                successfulPayment.Id,
                cancellationToken);

        foreach (Payment competingPayment in competingPayments)
        {
            StatementPaymentCheckoutSession? competingCheckout =
                await payments.FindCheckoutByPaymentAsync(competingPayment.Id, cancellationToken);
            if (!string.IsNullOrWhiteSpace(competingCheckout?.ProviderCheckoutSessionId))
            {
                try
                {
                    await stripe.ExpireCheckoutAsync(
                        competingCheckout.ProviderCheckoutSessionId,
                        cancellationToken);
                }
                catch (PaymentProviderUnavailableException)
                {
                    // The successful payment must still be committed. A late
                    // provider event is ignored once this attempt is cancelled.
                }
            }

            IReadOnlyCollection<PaymentPart> competingParts =
                await payments.ListPaymentPartsAsync(competingPayment.Id, cancellationToken);
            PaymentPart? educationPart = competingParts.SingleOrDefault(
                part => part.PaymentMethodCode == PaymentMethodCodes.EducationAccount);
            if (educationPart?.AccountHoldId is long holdId)
            {
                await accounts.ReleaseAsync(holdId, cancellationToken);
                educationPart.MarkCompleted(PaymentPartStatusCodes.Released, completedAtUtc);
            }
            competingParts.SingleOrDefault(part => part.PaymentMethodCode == PaymentMethodCodes.OnlinePayment)
                ?.MarkCompleted(PaymentPartStatusCodes.Failed, completedAtUtc);
            competingCheckout?.CancelBeforePayment(completedAtUtc);
            string beforeStatus = competingPayment.PaymentStatusCode;
            competingPayment.MarkCancelled(completedAtUtc);
            IReadOnlyCollection<PaymentAllocation> allocations =
                await payments.ListPaymentAllocationsAsync(competingPayment.Id, cancellationToken);
            await paymentAudit.RecordPaymentAsync(
                AuditActionCodes.PaymentCancelled,
                competingPayment,
                allocations.Select(x => x.BillId).ToArray(),
                "Payment cancelled",
                beforeStatus,
                competingPayment.PaymentStatusCode,
                "COMPETING_PAYMENT_CANCELLED",
                cancellationToken);
        }
    }

    private async Task RecordStatementFailureAsync(
        StatementPaymentCheckoutSession checkout,
        DateTime failedAtUtc,
        CancellationToken cancellationToken)
    {
        Payment payment = await payments.FindPaymentAsync(checkout.PaymentId!.Value, cancellationToken)
            ?? throw new InvalidOperationException("Statement payment was not found.");
        if (payment.PaymentStatusCode != PaymentStatusCodes.PendingOnlinePayment)
            return;
        string beforeStatus = payment.PaymentStatusCode;
        IReadOnlyCollection<PaymentPart> parts = await payments.ListPaymentPartsAsync(payment.Id, cancellationToken);
        PaymentPart? educationPart = parts.SingleOrDefault(x => x.PaymentMethodCode == PaymentMethodCodes.EducationAccount);
        if (educationPart?.AccountHoldId is long holdId)
        {
            await accounts.ReleaseAsync(holdId, cancellationToken);
            educationPart.MarkCompleted(PaymentPartStatusCodes.Released, failedAtUtc);
        }
        PaymentPart onlinePart = parts.Single(x => x.PaymentMethodCode == PaymentMethodCodes.OnlinePayment);
        onlinePart.MarkCompleted(PaymentPartStatusCodes.Failed, failedAtUtc);
        payment.MarkFailed(failedAtUtc);
        checkout.RecordPaymentFailure(failedAtUtc);
        IReadOnlyCollection<PaymentAllocation> allocations =
            await payments.ListPaymentAllocationsAsync(payment.Id, cancellationToken);
        await paymentAudit.RecordPaymentAsync(
            AuditActionCodes.PaymentFailed,
            payment,
            allocations.Select(x => x.BillId).ToArray(),
            "Payment failed",
            beforeStatus,
            payment.PaymentStatusCode,
            "STRIPE_WEBHOOK",
            cancellationToken);
        await fasNotifications.SendPaymentFailedAsync(payment.Id, cancellationToken);
        await paymentNotifications.SendStatementPaymentFailedAsync(
            payment,
            "The payment provider reported a failed payment. Please try again.",
            cancellationToken);
    }

    private async Task RecordStatementExpirationAsync(
        StatementPaymentCheckoutSession checkout,
        DateTime expiredAtUtc,
        CancellationToken cancellationToken)
    {
        Payment payment = await payments.FindPaymentAsync(checkout.PaymentId!.Value, cancellationToken)
            ?? throw new InvalidOperationException("Statement payment was not found.");
        if (payment.PaymentStatusCode != PaymentStatusCodes.PendingOnlinePayment)
            return;

        IReadOnlyCollection<PaymentPart> parts = await payments.ListPaymentPartsAsync(payment.Id, cancellationToken);
        PaymentPart? educationPart = parts.SingleOrDefault(
            part => part.PaymentMethodCode == PaymentMethodCodes.EducationAccount);
        if (educationPart?.AccountHoldId is long holdId)
        {
            await accounts.ReleaseAsync(holdId, cancellationToken);
            educationPart.MarkCompleted(PaymentPartStatusCodes.Released, expiredAtUtc);
        }
        parts.Single(part => part.PaymentMethodCode == PaymentMethodCodes.OnlinePayment)
            .MarkCompleted(PaymentPartStatusCodes.Failed, expiredAtUtc);
        payment.MarkExpired(expiredAtUtc);
        checkout.ExpireBeforePayment(expiredAtUtc);
        await paymentNotifications.SendPaymentExpiredAsync(payment, cancellationToken);
    }

    private async Task ApplyRefundAsync(ParsedPaymentWebhook webhook, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(webhook.ProviderChargeId)) return;
        Payment? payment = await payments.FindPaymentByChargeAsync(webhook.ProviderChargeId, cancellationToken);
        if (payment is null) return;
        decimal totalRefunded = webhook.AmountMinor / 100m;
        payment.ApplyProviderRefundTotal(totalRefunded);
        IReadOnlyCollection<PaymentRefund> refunds = await payments.ListRefundsAsync(payment.Id, cancellationToken);
        decimal remaining = totalRefunded;
        foreach (PaymentRefund refund in refunds.OrderBy(refund => refund.RequestedAtUtc))
        {
            if (remaining < refund.Amount) break;
            string beforeStatus = refund.RefundStatusCode;
            refund.MarkSucceeded(webhook.CreatedAtUtc);
            remaining -= refund.Amount;
            if (beforeStatus != refund.RefundStatusCode)
            {
                IReadOnlyCollection<PaymentAllocation> refundAllocations =
                    await payments.ListPaymentAllocationsAsync(payment.Id, cancellationToken);
                await paymentAudit.RecordRefundAsync(
                    AuditActionCodes.RefundCompleted,
                    payment,
                    refund.Id,
                    refundAllocations.Select(x => x.BillId).ToArray(),
                    "Refund completed",
                    "STRIPE_WEBHOOK",
                    cancellationToken);
            }
        }
        if (payment.PaymentStatusCode == PaymentStatusCodes.Refunded)
        {
            IReadOnlyCollection<PaymentAllocation> allocations =
                await payments.ListPaymentAllocationsAsync(payment.Id, cancellationToken);
            long[] allocatedBillIds = allocations
                .Select(allocation => allocation.BillId)
                .Distinct()
                .ToArray();

            if (allocatedBillIds.Length > 0)
            {
                await courses.ApplyFullRefundForBillsAsync(
                    allocatedBillIds,
                    webhook.CreatedAtUtc,
                    cancellationToken);
            }
            else
            {
                await courses.ApplyFullRefundAsync(payment.BillId, webhook.CreatedAtUtc, cancellationToken);
            }
        }
    }

    private async Task AttachCheckoutReferencesAsync(
        PaymentCheckoutSession checkout,
        ParsedPaymentWebhook webhook,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(webhook.ProviderPaymentIntentId))
            checkout.AttachPaymentIntent(webhook.ProviderPaymentIntentId, webhook.CreatedAtUtc);

        if (!checkout.IsInstallment || string.IsNullOrWhiteSpace(webhook.ProviderSubscriptionId)) return;
        if (string.IsNullOrWhiteSpace(checkout.ProviderPriceId))
            throw new InvalidOperationException("Payment checkout has no provider price.");

        StripeScheduleGatewayResult schedule = await stripe.AttachFiniteScheduleAsync(
            webhook.ProviderSubscriptionId,
            checkout.ProviderPriceId,
            checkout.RequiredInstallmentCount,
            cancellationToken);
        checkout.AttachSubscription(
            webhook.ProviderSubscriptionId,
            schedule.ProviderScheduleId,
            webhook.CreatedAtUtc);
    }

    private async Task RecordSuccessAsync(
        BillPaymentCheckoutSession checkout,
        ParsedPaymentWebhook webhook,
        CancellationToken cancellationToken)
    {
        string reference = webhook.ProviderInvoiceId ?? webhook.ProviderPaymentIntentId
            ?? throw new InvalidOperationException("Successful payment has no provider reference.");
        if (await payments.PaymentReferenceExistsAsync(reference, cancellationToken)) return;

        decimal amount = webhook.AmountMinor / 100m;
        bool changed = checkout.RecordSuccessfulPayment(webhook.CreatedAtUtc);
        if (!changed) return;

        string paymentIntent = webhook.ProviderPaymentIntentId
            ?? $"invoice:{webhook.ProviderInvoiceId ?? reference}";
        Payment payment = Payment.RecordProviderSuccess(
            checkout.BillId,
            checkout.PersonId,
            amount,
            paymentIntent,
            webhook.ProviderInvoiceId,
            webhook.ProviderChargeId,
            checkout.PaidInstallmentCount,
            webhook.CreatedAtUtc);
        await AttachProviderEvidenceAsync(payment, checkout, webhook, cancellationToken);
        await payments.AddPaymentAsync(payment, cancellationToken);
        await courses.ApplySuccessfulPaymentAsync(
            checkout.BillId,
            amount,
            checkout.CheckoutStatusCode == CheckoutStatusCodes.PaidInFull,
            webhook.CreatedAtUtc,
            cancellationToken);
        await fasSubsidies.RedeemPendingRedemptionsForBillsAsync(
            [checkout.BillId],
            webhook.CreatedAtUtc,
            cancellationToken);
        await paymentAudit.RecordPaymentAsync(
            AuditActionCodes.PaymentCompleted,
            payment,
            [checkout.BillId],
            "Payment completed",
            null,
            payment.PaymentStatusCode,
            "STRIPE_WEBHOOK",
            cancellationToken);
        await paymentNotifications.SendPaymentSucceededAsync(payment, webhook.CreatedAtUtc, cancellationToken);
    }

    private async Task AttachProviderEvidenceAsync(
        Payment payment,
        PaymentCheckoutSession checkout,
        ParsedPaymentWebhook webhook,
        CancellationToken cancellationToken)
    {
        try
        {
            StripePaymentEvidenceGatewayResult evidence = await stripe.GetPaymentEvidenceAsync(
                webhook.ProviderCheckoutSessionId ?? checkout.ProviderCheckoutSessionId,
                webhook.ProviderPaymentIntentId ?? payment.ProviderPaymentIntentId,
                webhook.ProviderInvoiceId ?? payment.ProviderInvoiceId,
                webhook.ProviderChargeId ?? payment.ProviderChargeId,
                cancellationToken);
            payment.AttachProviderEvidence(
                evidence.HostedInvoiceUrl,
                evidence.InvoicePdfUrl,
                evidence.ReceiptUrl,
                webhook.CreatedAtUtc);
        }
        catch (PaymentProviderUnavailableException exception)
        {
            logger.LogWarning(
                exception,
                "Stripe payment evidence could not be retrieved. PaymentId={PaymentId} CheckoutId={CheckoutId}",
                payment.Id,
                checkout.Id);
        }
    }
}
