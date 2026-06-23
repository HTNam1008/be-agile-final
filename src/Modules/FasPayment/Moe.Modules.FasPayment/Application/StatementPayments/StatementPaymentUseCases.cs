using FluentValidation;
using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Security;
using Moe.Modules.CourseBilling.IGateway.Payments;
using Moe.Modules.EducationAccountTopUp.IGateway.Accounts;
using Moe.Modules.FasPayment.Contracts.Payments;
using Moe.Modules.FasPayment.Domain.Payments;
using Moe.Modules.FasPayment.IGateway.Payments;
using Moe.SharedKernel.Results;
using Moe.Modules.FasPayment.Application;

namespace Moe.Modules.FasPayment.Application.StatementPayments;

internal sealed record PreviewStatementPaymentQuery(long StatementId) : IQuery<StatementPaymentPreviewResponse>;
internal sealed record PayBillingStatementCommand(long StatementId, PayBillingStatementRequest Request) : ICommand<PayBillingStatementResponse>;
internal sealed record CancelBillingStatementPaymentCommand(long StatementId, long PaymentId) : ICommand;
internal sealed record DeferBillingStatementCommand(long StatementId, DeferBillingStatementRequest Request) : ICommand;

internal sealed class PayBillingStatementRequestValidator : AbstractValidator<PayBillingStatementRequest>
{
    public PayBillingStatementRequestValidator()
        => RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(120);
}

internal sealed class PreviewStatementPaymentHandler(
    ICoursePaymentGateway billing,
    IEducationAccountPaymentGateway accounts,
    ICurrentUser currentUser)
    : IQueryHandler<PreviewStatementPaymentQuery, StatementPaymentPreviewResponse>
{
    public async Task<Result<StatementPaymentPreviewResponse>> Handle(PreviewStatementPaymentQuery query, CancellationToken ct)
    {
        if (!currentUser.TryGetStudent(out long personId))
            return Result<StatementPaymentPreviewResponse>.Failure(PaymentApplicationErrors.StudentRequired);

        PayableStatement? statement = await billing.FindPayableStatementAsync(query.StatementId, personId, ct);
        if (statement is null) return Result<StatementPaymentPreviewResponse>.Failure(PaymentApplicationErrors.BillNotFound);
        EducationAccountPaymentBalance? balance = await accounts.GetAvailableBalanceAsync(personId, ct);
        decimal available = balance?.AvailableBalance ?? 0m;
        decimal education = Math.Min(available, statement.OutstandingAmount);
        return Result<StatementPaymentPreviewResponse>.Success(new(
            statement.BillingStatementId, statement.OutstandingAmount,
            balance?.CurrentBalance ?? 0m, balance?.HeldBalance ?? 0m, available, education,
            statement.OutstandingAmount - education, statement.CurrencyCode));
    }
}

internal sealed class PayBillingStatementHandler(
    IPaymentCheckoutRepository payments,
    ICoursePaymentGateway billing,
    IEducationAccountPaymentGateway accounts,
    IStripePaymentGateway stripe,
    ICurrentUser currentUser,
    IClock clock) : ICommandHandler<PayBillingStatementCommand, PayBillingStatementResponse>
{
    public async Task<Result<PayBillingStatementResponse>> Handle(PayBillingStatementCommand command, CancellationToken ct)
    {
        if (!currentUser.TryGetStudent(out long personId))
            return Result<PayBillingStatementResponse>.Failure(PaymentApplicationErrors.StudentRequired);

        PayableStatement? statement = await billing.FindPayableStatementAsync(command.StatementId, personId, ct);
        if (statement is null) return Result<PayBillingStatementResponse>.Failure(PaymentApplicationErrors.BillNotFound);

        Result<PayBillingStatementResponse?> existingAttempt =
            await ResumeOrCloseExistingAttemptAsync(statement.BillingStatementId, personId, ct);
        if (!existingAttempt.IsSuccess)
            return Result<PayBillingStatementResponse>.Failure(existingAttempt.Error);
        if (existingAttempt.Value is not null)
            return Result<PayBillingStatementResponse>.Success(existingAttempt.Value);

        EducationAccountPaymentBalance? balance = await accounts.GetAvailableBalanceAsync(personId, ct);
        decimal educationAmount = Math.Min(balance?.AvailableBalance ?? 0m, statement.OutstandingAmount);
        decimal onlineAmount = statement.OutstandingAmount - educationAmount;
        DateTime now = clock.UtcNow.UtcDateTime;
        Payment payment = Payment.StartStatementPayment(statement.BillingStatementId, personId,
            statement.OutstandingAmount, educationAmount, onlineAmount, command.Request.IdempotencyKey, now);
        List<PaymentPart> parts = [];
        PaymentPart? educationPart = null;
        if (educationAmount > 0m)
        {
            educationPart = PaymentPart.Create(0, 1, PaymentMethodCodes.EducationAccount, educationAmount,
                onlineAmount > 0m ? PaymentPartStatusCodes.Reserved : PaymentPartStatusCodes.Pending, now);
            educationPart.AssignEducationAccount(balance!.EducationAccountId, null);
            parts.Add(educationPart);
        }
        if (onlineAmount > 0m)
            parts.Add(PaymentPart.Create(0, parts.Count + 1, PaymentMethodCodes.OnlinePayment, onlineAmount, PaymentPartStatusCodes.Pending, now));
        PaymentAllocation[] allocations = statement.Bills.Select(x =>
            new PaymentAllocation(0, x.BillId, x.BillingStatementItemId, x.OutstandingAmount, now)).ToArray();
        await payments.AddStatementPaymentAsync(payment, parts, allocations, ct);

        if (onlineAmount == 0m)
        {
            long transactionId = await accounts.DebitImmediatelyAsync(
                personId, educationPart!.Id, educationAmount, currentUser.UserAccountId, ct);
            educationPart.MarkCompleted(PaymentPartStatusCodes.Captured, now, transactionId);
            foreach (PaymentAllocation allocation in allocations) allocation.MarkApplied();
            await billing.ApplyStatementPaymentAsync(statement.BillingStatementId,
                allocations.Select(x => new BillPaymentAllocation(x.BillId, x.AllocatedAmount)).ToArray(), now, ct);
            payment.MarkSuccessful(now);
            await payments.ExecuteInTransactionAsync(_ => Task.CompletedTask, ct);
            return Result<PayBillingStatementResponse>.Success(new(
                payment.Id, payment.PaymentStatusCode, educationAmount, 0m, null, null, null, false));
        }

        if (educationPart is not null)
        {
            long holdId = await accounts.ReserveAsync(
                personId,
                educationPart.Id,
                educationAmount,
                now.Add(PaymentCheckoutPolicy.Lifetime),
                ct);
            educationPart.AssignAccountHold(holdId);
        }
        PaymentCheckoutSession checkout = PaymentCheckoutSession.CreateForStatement(
            payment.Id, statement.BillingStatementId, personId, onlineAmount, now);
        await payments.AddCheckoutAsync(checkout, ct);
        try
        {
            StripeCheckoutGatewayResult provider = await stripe.CreateCheckoutAsync(
                new StripeCheckoutGatewayRequest(checkout.IdempotencyKey, checkout.Id, 0, 0,
                    $"Monthly billing statement {statement.BillingStatementId}", statement.CurrencyCode,
                    decimal.ToInt64(onlineAmount * 100m), 1, null,
                    now.Add(PaymentCheckoutPolicy.Lifetime)), ct);
            checkout.AssignProviderCheckout(
                provider.ProviderSessionId,
                provider.ProviderPriceId,
                provider.CheckoutUrl,
                provider.ExpiresAtUtc,
                now);
            parts.Single(x => x.PaymentMethodCode == PaymentMethodCodes.OnlinePayment).AssignProvider("STRIPE", provider.ProviderSessionId);
            await payments.ExecuteInTransactionAsync(_ => Task.CompletedTask, ct);
            return Result<PayBillingStatementResponse>.Success(new(
                payment.Id,
                payment.PaymentStatusCode,
                educationAmount,
                onlineAmount,
                provider.CheckoutUrl,
                checkout.Id,
                provider.ExpiresAtUtc,
                false));
        }
        catch (PaymentProviderUnavailableException)
        {
            if (educationPart?.AccountHoldId is long holdId) await accounts.ReleaseAsync(holdId, ct);
            payment.MarkFailed(now);
            await payments.ExecuteInTransactionAsync(_ => Task.CompletedTask, ct);
            return Result<PayBillingStatementResponse>.Failure(PaymentDomainErrors.ProviderUnavailable);
        }
    }

    private async Task<Result<PayBillingStatementResponse?>> ResumeOrCloseExistingAttemptAsync(
        long billingStatementId,
        long personId,
        CancellationToken cancellationToken)
    {
        Payment? activePayment = await payments.FindActiveStatementPaymentAsync(
            billingStatementId,
            personId,
            cancellationToken);
        if (activePayment is null)
            return Result<PayBillingStatementResponse?>.Success(null);

        DateTime now = clock.UtcNow.UtcDateTime;
        PaymentCheckoutSession? checkout = await payments.FindCheckoutByPaymentAsync(
            activePayment.Id,
            cancellationToken);
        if (checkout?.CanResume(now) == true)
        {
            return Result<PayBillingStatementResponse?>.Success(new(
                activePayment.Id,
                activePayment.PaymentStatusCode,
                activePayment.EducationAccountAmount,
                activePayment.OnlinePaymentAmount,
                checkout.CheckoutUrl,
                checkout.Id,
                checkout.ExpiresAtUtc,
                true));
        }

        DateTime expiresAtUtc = checkout is null
            ? activePayment.InitiatedAtUtc.Add(PaymentCheckoutPolicy.Lifetime)
            : checkout.ExpiresAtUtc ?? now;
        if (expiresAtUtc > now)
            return Result<PayBillingStatementResponse?>.Failure(PaymentDomainErrors.StatementPaymentInProgress);

        if (!string.IsNullOrWhiteSpace(checkout?.ProviderCheckoutSessionId))
        {
            try
            {
                await stripe.ExpireCheckoutAsync(checkout.ProviderCheckoutSessionId, cancellationToken);
            }
            catch (PaymentProviderUnavailableException)
            {
                return Result<PayBillingStatementResponse?>.Failure(PaymentDomainErrors.ProviderUnavailable);
            }
        }

        IReadOnlyCollection<PaymentPart> parts = await payments.ListPaymentPartsAsync(
            activePayment.Id,
            cancellationToken);
        PaymentPart? educationPart = parts.SingleOrDefault(
            part => part.PaymentMethodCode == PaymentMethodCodes.EducationAccount);
        if (educationPart?.AccountHoldId is long holdId)
        {
            await accounts.ReleaseAsync(holdId, cancellationToken);
            educationPart.MarkCompleted(PaymentPartStatusCodes.Released, now);
        }
        PaymentPart? onlinePart = parts.SingleOrDefault(
            part => part.PaymentMethodCode == PaymentMethodCodes.OnlinePayment);
        onlinePart?.MarkCompleted(PaymentPartStatusCodes.Failed, now);
        checkout?.ExpireBeforePayment(now);
        activePayment.MarkExpired(now);
        await payments.ExecuteInTransactionAsync(_ => Task.CompletedTask, cancellationToken);
        return Result<PayBillingStatementResponse?>.Success(null);
    }
}

internal sealed class CancelBillingStatementPaymentHandler(
    IPaymentCheckoutRepository payments,
    IEducationAccountPaymentGateway accounts,
    IStripePaymentGateway stripe,
    ICurrentUser currentUser,
    IClock clock) : ICommandHandler<CancelBillingStatementPaymentCommand>
{
    public async Task<Result> Handle(CancelBillingStatementPaymentCommand command, CancellationToken ct)
    {
        if (!currentUser.TryGetStudent(out long personId))
            return Result.Failure(PaymentApplicationErrors.StudentRequired);

        Payment? payment = await payments.FindPaymentAsync(command.PaymentId, ct);
        if (payment is null ||
            payment.BillingStatementId != command.StatementId ||
            payment.PayerPersonId != personId)
            return Result.Failure(PaymentApplicationErrors.BillNotFound);

        if (payment.PaymentStatusCode is not (PaymentStatusCodes.Initiated or PaymentStatusCodes.PendingOnlinePayment))
            return Result.Success();

        PaymentCheckoutSession? checkout = await payments.FindCheckoutByPaymentAsync(payment.Id, ct);
        if (!string.IsNullOrWhiteSpace(checkout?.ProviderCheckoutSessionId))
        {
            try
            {
                await stripe.ExpireCheckoutAsync(checkout.ProviderCheckoutSessionId, ct);
            }
            catch (PaymentProviderUnavailableException)
            {
                return Result.Failure(PaymentDomainErrors.ProviderUnavailable);
            }
        }

        DateTime now = clock.UtcNow.UtcDateTime;
        IReadOnlyCollection<PaymentPart> parts = await payments.ListPaymentPartsAsync(payment.Id, ct);
        PaymentPart? educationPart = parts.SingleOrDefault(
            part => part.PaymentMethodCode == PaymentMethodCodes.EducationAccount);
        if (educationPart?.AccountHoldId is long holdId)
        {
            await accounts.ReleaseAsync(holdId, ct);
            educationPart.MarkCompleted(PaymentPartStatusCodes.Released, now);
        }
        parts.SingleOrDefault(part => part.PaymentMethodCode == PaymentMethodCodes.OnlinePayment)
            ?.MarkCompleted(PaymentPartStatusCodes.Failed, now);
        checkout?.CancelBeforePayment(now);
        payment.MarkCancelled(now);
        await payments.ExecuteInTransactionAsync(_ => Task.CompletedTask, ct);
        return Result.Success();
    }
}

internal sealed class DeferBillingStatementHandler(
    IPaymentCheckoutRepository payments,
    ICoursePaymentGateway billing,
    ICurrentUser currentUser,
    IClock clock) : ICommandHandler<DeferBillingStatementCommand>
{
    public async Task<Result> Handle(DeferBillingStatementCommand command, CancellationToken ct)
    {
        if (!currentUser.TryGetStudent(out long personId) || currentUser.UserAccountId is not long actorId)
            return Result.Failure(PaymentApplicationErrors.StudentRequired);

        Payment? payment = await payments.FindPaymentAsync(command.Request.FailedPaymentId, ct);
        if (payment is null || payment.BillingStatementId != command.StatementId ||
            payment.PayerPersonId != personId ||
            payment.PaymentStatusCode is not (PaymentStatusCodes.Failed or PaymentStatusCodes.Expired or PaymentStatusCodes.Cancelled))
            return Result.Failure(PaymentDomainErrors.InvalidDeferral);
        await billing.DeferStatementAsync(command.StatementId, personId, payment.Id, actorId, clock.UtcNow.UtcDateTime, ct);
        return Result.Success();
    }
}
