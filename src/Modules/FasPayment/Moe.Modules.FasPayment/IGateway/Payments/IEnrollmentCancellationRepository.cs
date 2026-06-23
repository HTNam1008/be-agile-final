using Moe.SharedKernel.Results;

namespace Moe.Modules.FasPayment.IGateway.Payments;

internal interface IEnrollmentCancellationRepository
{
    Task<Result<string>> CancelEnrollmentAndOutstandingBillsAsync(
        long enrollmentId,
        long personId,
        bool refunded,
        DateTime utcNow,
        CancellationToken cancellationToken);
}

