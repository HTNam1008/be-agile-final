using FluentValidation;
using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Security;
using Moe.Modules.FasPayment.Contracts.Payments;
using Moe.Modules.FasPayment.Domain.Payments;
using Moe.Modules.FasPayment.IGateway.Payments;
using Moe.SharedKernel.Results;

namespace Moe.Modules.FasPayment.Application.AdminPayments;

internal sealed record ListAdminPaymentsQuery : IQuery<IReadOnlyCollection<AdminPaymentResponse>>;
internal sealed record ListPaymentWebhookEventsQuery : IQuery<IReadOnlyCollection<PaymentWebhookEventResponse>>;
internal sealed record CreatePaymentRefundCommand(long PaymentId, CreatePaymentRefundRequest Request)
    : ICommand<PaymentRefundResponse>;

internal sealed class CreatePaymentRefundRequestValidator : AbstractValidator<CreatePaymentRefundRequest>
{
    public CreatePaymentRefundRequestValidator()
    {
        RuleFor(request => request.Amount).GreaterThan(0m);
        RuleFor(request => request.Reason).NotEmpty().MaximumLength(500);
    }
}

internal sealed class ListAdminPaymentsHandler(IPaymentCheckoutRepository payments, ICurrentUser currentUser)
    : IQueryHandler<ListAdminPaymentsQuery, IReadOnlyCollection<AdminPaymentResponse>>
{
    public async Task<Result<IReadOnlyCollection<AdminPaymentResponse>>> Handle(
        ListAdminPaymentsQuery query,
        CancellationToken cancellationToken)
    {
        if (!IsAdmin(currentUser))
            return Result<IReadOnlyCollection<AdminPaymentResponse>>.Failure(PaymentApplicationErrors.AdministratorRequired);
        IReadOnlyCollection<Payment> items = await payments.ListPaymentsAsync(cancellationToken);
        return Result<IReadOnlyCollection<AdminPaymentResponse>>.Success(items.Select(payment => new AdminPaymentResponse(
            payment.Id,
            payment.BillId,
            payment.PayerPersonId,
            payment.PaymentAmount,
            payment.SuccessfulAmount,
            payment.PaymentStatusCode,
            payment.ProviderChargeId,
            payment.InitiatedAtUtc)).ToArray());
    }

    internal static bool IsAdmin(ICurrentUser currentUser)
        => currentUser.IsAuthenticated && currentUser.Roles.Any(role => role is FasRoles.HqAdmin or FasRoles.SchoolAdmin);
}

internal sealed class ListPaymentWebhookEventsHandler(IPaymentCheckoutRepository payments, ICurrentUser currentUser)
    : IQueryHandler<ListPaymentWebhookEventsQuery, IReadOnlyCollection<PaymentWebhookEventResponse>>
{
    public async Task<Result<IReadOnlyCollection<PaymentWebhookEventResponse>>> Handle(
        ListPaymentWebhookEventsQuery query,
        CancellationToken cancellationToken)
    {
        if (!ListAdminPaymentsHandler.IsAdmin(currentUser))
            return Result<IReadOnlyCollection<PaymentWebhookEventResponse>>.Failure(PaymentApplicationErrors.AdministratorRequired);
        IReadOnlyCollection<ProcessedPaymentWebhookEvent> items = await payments.ListWebhookEventsAsync(cancellationToken);
        return Result<IReadOnlyCollection<PaymentWebhookEventResponse>>.Success(items.Select(item => new PaymentWebhookEventResponse(
            item.Id,
            item.ProviderEventId,
            item.EventType,
            item.ProcessingStatusCode,
            item.ReceivedAtUtc)).ToArray());
    }
}

internal sealed class CreatePaymentRefundHandler(
    IPaymentCheckoutRepository payments,
    IStripePaymentGateway stripe,
    ICurrentUser currentUser,
    IClock clock) : ICommandHandler<CreatePaymentRefundCommand, PaymentRefundResponse>
{
    public async Task<Result<PaymentRefundResponse>> Handle(
        CreatePaymentRefundCommand command,
        CancellationToken cancellationToken)
    {
        if (!ListAdminPaymentsHandler.IsAdmin(currentUser) || currentUser.UserAccountId is not long actorId)
            return Result<PaymentRefundResponse>.Failure(PaymentApplicationErrors.AdministratorRequired);

        Payment? payment = await payments.FindPaymentAsync(command.PaymentId, cancellationToken);
        if (payment is null || string.IsNullOrWhiteSpace(payment.ProviderChargeId))
            return Result<PaymentRefundResponse>.Failure(PaymentDomainErrors.PaymentNotFound);
        decimal reserved = await payments.GetSucceededRefundAmountAsync(payment.Id, cancellationToken);
        if (command.Request.Amount > payment.PaymentAmount - reserved)
            return Result<PaymentRefundResponse>.Failure(PaymentDomainErrors.RefundExceedsPayment);

        Result<PaymentRefund> created = PaymentRefund.Create(
            payment.Id,
            command.Request.Amount,
            command.Request.Reason,
            actorId,
            clock.UtcNow.UtcDateTime);
        if (created.IsFailure) return Result<PaymentRefundResponse>.Failure(created.Error);

        try
        {
            StripeRefundGatewayResult provider = await stripe.CreateRefundAsync(
                $"refund:{payment.Id}:{decimal.ToInt64(command.Request.Amount * 100m)}:{actorId}",
                payment.ProviderChargeId,
                decimal.ToInt64(command.Request.Amount * 100m),
                cancellationToken);
            created.Value.AssignProviderRefund(provider.ProviderRefundId);
            await payments.AddRefundAsync(created.Value, cancellationToken);
        }
        catch (PaymentProviderUnavailableException)
        {
            return Result<PaymentRefundResponse>.Failure(PaymentDomainErrors.ProviderUnavailable);
        }

        return Result<PaymentRefundResponse>.Success(new(
            created.Value.Id,
            created.Value.PaymentId,
            created.Value.Amount,
            created.Value.RefundStatusCode));
    }
}
