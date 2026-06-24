using Moe.Modules.FasPayment.Contracts.Payments;
using Moe.Modules.FasPayment.IGateway.Payments;
using Moe.SharedKernel.Results;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.FasPayment.Infrastructure.Payments;

internal sealed class LegacyCoursePaymentGateway(MoeDbContext dbContext) : ILegacyCoursePaymentGateway
{
    public Task<OutstandingBillsResponse> ReadOutstandingBillsAsync(
        long personId,
        CancellationToken cancellationToken)
        => PaymentSqlReader.ReadOutstandingBillsAsync(dbContext, personId, cancellationToken);

    public Task<Result<PayBillResponse>> PayBillAsync(
        long personId,
        long? userAccountId,
        PayBillRequest request,
        CancellationToken cancellationToken)
        => PaymentSqlWriter.PayBillAsync(dbContext, personId, userAccountId, request, cancellationToken);
}
