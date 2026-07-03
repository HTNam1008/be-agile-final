using Moe.Modules.FasPayment.Domain.Payments;

namespace Moe.Modules.FasPayment.Application.Audit;

internal interface IPaymentSchoolAuditRecorder
{
    Task RecordPaymentAsync(
        string actionCode,
        Payment payment,
        IReadOnlyCollection<long> billIds,
        string summary,
        string? beforeStatus,
        string afterStatus,
        string reasonCode,
        CancellationToken cancellationToken);

    Task RecordRefundAsync(
        string actionCode,
        Payment payment,
        long refundId,
        IReadOnlyCollection<long> billIds,
        string summary,
        string reasonCode,
        CancellationToken cancellationToken);
}
