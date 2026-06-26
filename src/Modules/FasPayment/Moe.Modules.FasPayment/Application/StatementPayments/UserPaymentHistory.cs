using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Security;
using Moe.Modules.FasPayment.Contracts.Payments;
using Moe.Modules.FasPayment.Domain.Payments;
using Moe.Modules.FasPayment.IGateway.Payments;
using Moe.SharedKernel.Results;

namespace Moe.Modules.FasPayment.Application.StatementPayments;

public sealed record ListUserPaymentHistoryQuery
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

        long[] paymentIds = history.Select(payment => payment.Id).ToArray();
        IReadOnlyCollection<PaymentPart> parts =
            await payments.ListPaymentPartsForPaymentsAsync(paymentIds, cancellationToken);
        IReadOnlyCollection<PaymentRefund> paymentRefunds =
            await payments.ListPaymentRefundsForPaymentsAsync(paymentIds, cancellationToken);
        IReadOnlyCollection<EnrollmentRefundPart> enrollmentRefundParts =
            await payments.ListEnrollmentRefundPartsForPaymentsAsync(paymentIds, cancellationToken);
        IReadOnlyCollection<UserFasSettlement> fasSettlements =
            await payments.ListRedeemedFasSettlementsForPersonAsync(personId, cancellationToken);

        ILookup<long, PaymentPart> partsByPayment = parts.ToLookup(part => part.PaymentId);
        ILookup<long, PaymentRefund> paymentRefundsByPayment = paymentRefunds.ToLookup(refund => refund.PaymentId);
        ILookup<long, EnrollmentRefundPart> enrollmentRefundPartsByPayment =
            enrollmentRefundParts
                .Where(part => part.PaymentId is not null)
                .ToLookup(part => part.PaymentId!.Value);

        UserPaymentHistoryResponse[] paymentRows = history.Select(payment => new UserPaymentHistoryResponse(
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
                payment.FailedAtUtc,
                partsByPayment[payment.Id]
                    .Select(part => new UserPaymentHistoryPartResponse(
                        part.Id,
                        part.SequenceNumber,
                        part.PaymentMethodCode,
                        ToFundingSourceCode(part.PaymentMethodCode),
                        part.EducationAccountId,
                        part.AccountTransactionId,
                        part.PartAmount,
                        part.ProviderCode,
                        part.ProviderReference,
                        part.PartStatusCode,
                        part.CreatedAtUtc,
                        part.CompletedAtUtc,
                        part.SettledAtUtc,
                        part.FailureReason))
                    .ToArray(),
                BuildRefundResponses(
                    paymentRefundsByPayment[payment.Id],
                    enrollmentRefundPartsByPayment[payment.Id]),
                []))
            .ToArray();

        UserPaymentHistoryResponse[] fasRows = fasSettlements
            .Select(settlement => new UserPaymentHistoryResponse(
                -settlement.FasVoucherRedemptionId,
                $"FAS-{settlement.FasVoucherRedemptionId:D8}",
                null,
                0m,
                0m,
                0m,
                0m,
                PaymentStatusCodes.Successful,
                $"FAS-{settlement.FasApplicationSchemeId}",
                settlement.RedeemedAtUtc ?? settlement.CreatedAtUtc,
                settlement.RedeemedAtUtc ?? settlement.CreatedAtUtc,
                null,
                [],
                [],
                [ToFasSettlementResponse(settlement)]))
            .ToArray();

        return Result<IReadOnlyCollection<UserPaymentHistoryResponse>>.Success(
            paymentRows
                .Concat(fasRows)
                .OrderByDescending(row => row.CompletedAtUtc ?? row.FailedAtUtc ?? row.InitiatedAtUtc)
                .Take(150)
                .ToArray());
    }

    private static UserPaymentHistoryFasSettlementResponse ToFasSettlementResponse(UserFasSettlement settlement)
        => new(
            settlement.FasVoucherRedemptionId,
            settlement.FasApplicationSchemeId,
            settlement.CourseId,
            settlement.CourseEnrollmentId,
            settlement.BillId,
            settlement.BillNumber,
            settlement.CourseCode,
            settlement.CourseName,
            settlement.SchemeName,
            settlement.AppliedAmount,
            settlement.StatusCode,
            settlement.CreatedAtUtc,
            settlement.RedeemedAtUtc);

    private static IReadOnlyCollection<UserPaymentHistoryRefundResponse> BuildRefundResponses(
        IEnumerable<PaymentRefund> paymentRefunds,
        IEnumerable<EnrollmentRefundPart> enrollmentRefundParts)
    {
        UserPaymentHistoryRefundResponse[] legacyRefunds = paymentRefunds
            .Select(refund => new UserPaymentHistoryRefundResponse(
                refund.Id,
                null,
                "ONLINE_PAYMENT",
                refund.Amount,
                refund.RefundStatusCode,
                refund.ProviderRefundId,
                null,
                refund.RequestedAtUtc,
                refund.CompletedAtUtc,
                null))
            .ToArray();

        UserPaymentHistoryRefundResponse[] enrollmentRefunds = enrollmentRefundParts
            .Select(part => new UserPaymentHistoryRefundResponse(
                part.Id,
                part.PaymentPartId,
                part.RefundMethodCode,
                part.RefundAmount,
                part.RefundStatusCode,
                part.ProviderRefundId,
                part.AccountTransactionId,
                part.CreatedAtUtc,
                part.CompletedAtUtc,
                part.FailureReason))
            .ToArray();

        return legacyRefunds
            .Concat(enrollmentRefunds)
            .OrderByDescending(refund => refund.CreatedAtUtc)
            .ToArray();
    }

    private static string ToFundingSourceCode(string paymentMethodCode)
        => paymentMethodCode == PaymentMethodCodes.EducationAccount
            ? PaymentMethodCodes.EducationAccount
            : PaymentMethodCodes.OnlinePayment;
}
