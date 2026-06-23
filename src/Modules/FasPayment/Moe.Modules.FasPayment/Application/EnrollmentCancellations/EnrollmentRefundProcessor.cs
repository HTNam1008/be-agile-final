using Moe.Application.Abstractions.Clock;
using Moe.Modules.EducationAccountTopUp.IGateway.Accounts;
using Moe.Modules.FasPayment.Domain.Payments;
using Moe.Modules.FasPayment.IGateway.Payments;
using Moe.SharedKernel.Results;

namespace Moe.Modules.FasPayment.Application.EnrollmentCancellations;

internal sealed record EnrollmentRefundExecutionResult(
    long EnrollmentRefundId,
    string RefundStatusCode,
    decimal RefundAmount,
    decimal EducationAccountRefundAmount,
    decimal OnlineRefundAmount);

internal interface IEnrollmentRefundProcessor
{
    Task<Result<EnrollmentRefundExecutionResult>> ExecuteAsync(
        EnrollmentCancellationSnapshot snapshot,
        EnrollmentRefundCalculation calculation,
        string idempotencyKey,
        long actorUserAccountId,
        CancellationToken cancellationToken);
}

internal sealed class EnrollmentRefundProcessor(
    IPaymentCheckoutRepository payments,
    IEducationAccountPaymentGateway accounts,
    IStripePaymentGateway stripe,
    IClock clock) : IEnrollmentRefundProcessor
{
    public async Task<Result<EnrollmentRefundExecutionResult>> ExecuteAsync(
        EnrollmentCancellationSnapshot snapshot,
        EnrollmentRefundCalculation calculation,
        string idempotencyKey,
        long actorUserAccountId,
        CancellationToken cancellationToken)
    {
        EnrollmentRefund? existing = await payments.FindEnrollmentRefundByIdempotencyKeyAsync(
            idempotencyKey,
            cancellationToken);
        if (existing?.RefundStatusCode == EnrollmentRefundStatusCodes.Succeeded)
            return Result<EnrollmentRefundExecutionResult>.Success(ToResult(existing));

        DateTime now = clock.UtcNow.UtcDateTime;
        EnrollmentRefund refund;
        if (existing is null)
        {
            Result<EnrollmentRefund> created = EnrollmentRefund.Create(
                snapshot.Enrollment.Id,
                snapshot.Enrollment.PersonId,
                snapshot.PaidAmount,
                calculation.RefundPercentage,
                calculation.RefundAmount,
                calculation.EducationAccountRefundAmount,
                calculation.OnlineRefundAmount,
                calculation.PolicyPeriodCode,
                idempotencyKey,
                actorUserAccountId,
                now);
            if (created.IsFailure)
                return Result<EnrollmentRefundExecutionResult>.Failure(created.Error);

            refund = created.Value;
            await payments.AddEnrollmentRefundAsync(refund, cancellationToken);
        }
        else
        {
            refund = existing;
        }

        try
        {
            foreach (EnrollmentPaymentRefundSource source in snapshot.RefundSources)
            {
                decimal educationRefund = Money(
                    source.EducationAccountAllocatedAmount * calculation.RefundPercentage / 100m);
                if (educationRefund > 0m)
                {
                    string partKey = $"{idempotencyKey}:EA:{source.PaymentId}";
                    EnrollmentRefundPart? existingPart =
                        await payments.FindEnrollmentRefundPartByIdempotencyKeyAsync(
                            partKey,
                            cancellationToken);
                    if (existingPart?.RefundStatusCode == EnrollmentRefundStatusCodes.Succeeded)
                        continue;
                    EnrollmentRefundPart part = existingPart ?? EnrollmentRefundPart.Create(
                            refund.Id,
                            source.PaymentId,
                            source.EducationAccountPaymentPartId,
                            EnrollmentRefundMethodCodes.EducationAccount,
                            educationRefund,
                            partKey,
                            now);
                    if (existingPart is null)
                        await payments.AddEnrollmentRefundPartAsync(part, cancellationToken);
                    long transactionId = await accounts.CreditRefundAsync(
                        snapshot.Enrollment.PersonId,
                        refund.Id,
                        educationRefund,
                        source.EducationAccountTransactionId,
                        partKey,
                        actorUserAccountId,
                        cancellationToken);
                    part.MarkEducationAccountSucceeded(transactionId, clock.UtcNow.UtcDateTime);
                    await payments.ExecuteInTransactionAsync(_ => Task.CompletedTask, cancellationToken);
                }

                decimal onlineRefund = Money(
                    source.OnlineAllocatedAmount * calculation.RefundPercentage / 100m);
                if (onlineRefund > 0m)
                {
                    if (string.IsNullOrWhiteSpace(source.ProviderChargeId))
                        throw new PaymentProviderUnavailableException();

                    string partKey = $"{idempotencyKey}:STRIPE:{source.PaymentId}";
                    EnrollmentRefundPart? existingPart =
                        await payments.FindEnrollmentRefundPartByIdempotencyKeyAsync(
                            partKey,
                            cancellationToken);
                    if (existingPart?.RefundStatusCode == EnrollmentRefundStatusCodes.Succeeded)
                        continue;
                    EnrollmentRefundPart part = existingPart ?? EnrollmentRefundPart.Create(
                            refund.Id,
                            source.PaymentId,
                            null,
                            EnrollmentRefundMethodCodes.Stripe,
                            onlineRefund,
                            partKey,
                            now);
                    if (existingPart is null)
                        await payments.AddEnrollmentRefundPartAsync(part, cancellationToken);
                    StripeRefundGatewayResult provider = await stripe.CreateRefundAsync(
                        partKey,
                        source.ProviderChargeId,
                        decimal.ToInt64(onlineRefund * 100m),
                        cancellationToken);
                    part.MarkStripeSucceeded(provider.ProviderRefundId, clock.UtcNow.UtcDateTime);
                    await payments.ExecuteInTransactionAsync(_ => Task.CompletedTask, cancellationToken);
                }
            }

            refund.MarkSucceeded(clock.UtcNow.UtcDateTime);
            await payments.ExecuteInTransactionAsync(_ => Task.CompletedTask, cancellationToken);
            return Result<EnrollmentRefundExecutionResult>.Success(ToResult(refund));
        }
        catch (PaymentProviderUnavailableException)
        {
            refund.MarkFailed(PaymentDomainErrors.ProviderUnavailable.Message);
            await payments.ExecuteInTransactionAsync(_ => Task.CompletedTask, cancellationToken);
            return Result<EnrollmentRefundExecutionResult>.Failure(PaymentDomainErrors.ProviderUnavailable);
        }
    }

    private static EnrollmentRefundExecutionResult ToResult(EnrollmentRefund refund)
        => new(
            refund.Id,
            refund.RefundStatusCode,
            refund.RefundAmount,
            refund.EducationAccountRefundAmount,
            refund.OnlineRefundAmount);

    private static decimal Money(decimal amount)
        => decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
}
