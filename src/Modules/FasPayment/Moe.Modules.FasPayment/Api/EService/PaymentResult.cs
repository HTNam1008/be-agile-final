using Moe.Modules.FasPayment.Contracts.Payments;
using Moe.SharedKernel.Results;

namespace Moe.Modules.FasPayment.Api.EService;

internal sealed record PaymentResult(
    bool IsFailure,
    Error Error,
    int StatusCode,
    PayBillResponse? Response)
{
    public static PaymentResult Failure(Error error, int statusCode)
        => new(true, error, statusCode, null);

    public static PaymentResult Success(PayBillResponse response)
        => new(false, Error.None, 200, response);
}
