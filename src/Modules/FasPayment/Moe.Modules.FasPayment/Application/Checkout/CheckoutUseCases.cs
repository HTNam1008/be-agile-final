using FluentValidation;
using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Security;
using Moe.Modules.CourseBilling.IGateway.Payments;
using Moe.Modules.FasPayment.Application;
using Moe.Modules.FasPayment.Contracts.Payments;
using Moe.Modules.FasPayment.Domain.Payments;
using Moe.Modules.FasPayment.IGateway.Payments;
using Moe.SharedKernel.Results;

namespace Moe.Modules.FasPayment.Application.Checkout;

internal sealed record CreateStripeCheckoutCommand(CreateStripeCheckoutRequest Request)
    : ICommand<StripeCheckoutResponse>;

internal sealed record GetPaymentCheckoutStatusQuery(long CheckoutId)
    : IQuery<PaymentCheckoutStatusResponse>;

internal sealed class CreateStripeCheckoutRequestValidator : AbstractValidator<CreateStripeCheckoutRequest>
{
    public CreateStripeCheckoutRequestValidator()
    {
        RuleFor(request => request.BillId).GreaterThan(0);
        RuleFor(request => request.CoursePaymentPlanId).GreaterThan(0);
    }
}

internal sealed class CreateStripeCheckoutHandler(
    IPaymentCheckoutRepository payments,
    ICoursePaymentGateway courses,
    IStripePaymentGateway stripe,
    ICurrentUser currentUser,
    IClock clock) : ICommandHandler<CreateStripeCheckoutCommand, StripeCheckoutResponse>
{
    public async Task<Result<StripeCheckoutResponse>> Handle(
        CreateStripeCheckoutCommand command,
        CancellationToken cancellationToken)
    {
        if (!currentUser.TryGetStudent(out long personId))
            return Result<StripeCheckoutResponse>.Failure(PaymentApplicationErrors.StudentRequired);

        PayableCourseBill? bill = await courses.FindPayableBillAsync(
            command.Request.BillId,
            personId,
            cancellationToken);
        if (bill is null)
            return Result<StripeCheckoutResponse>.Failure(PaymentApplicationErrors.BillNotFound);

        CoursePaymentPlan? plan = await payments.FindPlanAsync(
            command.Request.CoursePaymentPlanId,
            cancellationToken);
        if (plan is null || !plan.IsActive || plan.CourseId != bill.CourseId)
            return Result<StripeCheckoutResponse>.Failure(PaymentDomainErrors.PaymentPlanNotFound);

        PaymentCheckoutSession? checkout = await payments.FindOpenCheckoutAsync(
            bill.BillId,
            personId,
            cancellationToken);
        if (checkout is not null && checkout.CoursePaymentPlanId != plan.Id)
        {
            if (!checkout.CancelBeforePayment(clock.UtcNow.UtcDateTime))
                return Result<StripeCheckoutResponse>.Failure(PaymentDomainErrors.CheckoutConflict);

            if (!string.IsNullOrWhiteSpace(checkout.ProviderCheckoutSessionId))
            {
                try
                {
                    await stripe.ExpireCheckoutAsync(
                        checkout.ProviderCheckoutSessionId,
                        cancellationToken);
                }
                catch (PaymentProviderUnavailableException)
                {
                    return Result<StripeCheckoutResponse>.Failure(PaymentDomainErrors.ProviderUnavailable);
                }
            }

            await payments.ExecuteInTransactionAsync(_ => Task.CompletedTask, cancellationToken);
            checkout = null;
        }

        if (checkout is null)
        {
            Result<PaymentCheckoutSession> created = PaymentCheckoutSession.Create(
                bill.BillId,
                bill.CourseEnrollmentId,
                bill.CourseId,
                personId,
                plan,
                bill.OutstandingAmount,
                clock.UtcNow.UtcDateTime);
            if (created.IsFailure) return Result<StripeCheckoutResponse>.Failure(created.Error);
            checkout = created.Value;
            await payments.AddCheckoutAsync(checkout, cancellationToken);
        }
        else if (checkout.CanResume(clock.UtcNow.UtcDateTime))
        {
            return Result<StripeCheckoutResponse>.Success(new(
                checkout.Id,
                checkout.CheckoutUrl!,
                checkout.CheckoutStatusCode));
        }

        long amountMinor = decimal.ToInt64(checkout.Amount * 100m);
        StripeCheckoutGatewayResult provider;
        try
        {
            provider = await stripe.CreateCheckoutAsync(
                new StripeCheckoutGatewayRequest(
                checkout.IdempotencyKey,
                checkout.Id,
                checkout.CourseId,
                checkout.BillId,
                bill.CourseName,
                checkout.CurrencyCode,
                amountMinor / checkout.RequiredInstallmentCount,
                checkout.RequiredInstallmentCount,
                    checkout.ProviderPriceId,
                    clock.UtcNow.UtcDateTime.Add(PaymentCheckoutPolicy.Lifetime)),
                cancellationToken);
        }
        catch (PaymentProviderUnavailableException)
        {
            return Result<StripeCheckoutResponse>.Failure(PaymentDomainErrors.ProviderUnavailable);
        }
        checkout.AssignProviderCheckout(
            provider.ProviderSessionId,
            provider.ProviderPriceId,
            provider.CheckoutUrl,
            provider.ExpiresAtUtc,
            clock.UtcNow.UtcDateTime);
        await payments.ExecuteInTransactionAsync(_ => Task.CompletedTask, cancellationToken);

        return Result<StripeCheckoutResponse>.Success(new(
            checkout.Id,
            provider.CheckoutUrl,
            checkout.CheckoutStatusCode));
    }
}

internal sealed class GetPaymentCheckoutStatusHandler(
    IPaymentCheckoutRepository payments,
    ICurrentUser currentUser)
    : IQueryHandler<GetPaymentCheckoutStatusQuery, PaymentCheckoutStatusResponse>
{
    public async Task<Result<PaymentCheckoutStatusResponse>> Handle(
        GetPaymentCheckoutStatusQuery query,
        CancellationToken cancellationToken)
    {
        if (!currentUser.TryGetStudent(out long personId))
            return Result<PaymentCheckoutStatusResponse>.Failure(PaymentApplicationErrors.StudentRequired);

        PaymentCheckoutSession? checkout = await payments.FindCheckoutAsync(query.CheckoutId, personId, cancellationToken);
        return checkout is null
            ? Result<PaymentCheckoutStatusResponse>.Failure(PaymentDomainErrors.CheckoutNotFound)
            : Result<PaymentCheckoutStatusResponse>.Success(new(
                checkout.Id,
                checkout.BillId,
                checkout.CheckoutStatusCode,
                checkout.PaidInstallmentCount,
                checkout.RequiredInstallmentCount));
    }
}
