using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Security;
using Moe.Modules.FasPayment.Contracts.Payments;
using Moe.Modules.FasPayment.Domain.Payments;
using Moe.Modules.FasPayment.IGateway.Payments;
using Moe.SharedKernel.Results;

namespace Moe.Modules.FasPayment.Application.StatementPayments;

internal sealed record ListUserPaymentHistoryQuery
    : IQuery<IReadOnlyCollection<UserPaymentHistoryResponse>>;

internal sealed class ListUserPaymentHistoryHandler(
    IPaymentCheckoutRepository payments,
    ICurrentUser currentUser)
    : IQueryHandler<ListUserPaymentHistoryQuery, IReadOnlyCollection<UserPaymentHistoryResponse>>
{
    public async Task<Result<IReadOnlyCollection<UserPaymentHistoryResponse>>> Handle(
        ListUserPaymentHistoryQuery query,
        CancellationToken cancellationToken)
    {
        long personId = currentUser.PersonId ?? 0;
        if (!currentUser.IsAuthenticated || personId <= 0)
            return Result<IReadOnlyCollection<UserPaymentHistoryResponse>>.Failure(
                PaymentApplicationErrors.StudentRequired);

        IReadOnlyCollection<Payment> history =
            await payments.ListPaymentsForPersonAsync(personId, cancellationToken);

        return Result<IReadOnlyCollection<UserPaymentHistoryResponse>>.Success(
            history.Select(payment => new UserPaymentHistoryResponse(
                payment.Id,
                payment.PaymentNumber,
                payment.BillingStatementId,
                payment.PaymentAmount,
                payment.SuccessfulAmount,
                payment.EducationAccountAmount,
                payment.OnlinePaymentAmount,
                payment.PaymentStatusCode,
                payment.ReceiptNumber,
                payment.InitiatedAtUtc,
                payment.CompletedAtUtc,
                payment.FailedAtUtc))
            .ToArray());
    }
}
