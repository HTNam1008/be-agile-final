using Microsoft.AspNetCore.Mvc;
using Moe.Infrastructure.Shared.Api;
using Moe.SharedKernel.Results;

namespace Moe.Modules.FasPayment.Api;

internal static class PaymentApiResponses
{
    public static IActionResult ToPaymentResponse<T>(this ControllerBase controller, Result<T> result, bool created = false)
        => result.IsSuccess
            ? created
                ? ApiResponseFactory.Created(result.Value, controller.HttpContext.TraceIdentifier)
                : ApiResponseFactory.Ok(result.Value, controller.HttpContext.TraceIdentifier)
            : ApiResponseFactory.Failure(result.Error, StatusCode(result.Error), controller.HttpContext.TraceIdentifier);

    public static IActionResult ToPaymentResponse(this ControllerBase controller, Result result)
        => result.IsSuccess
            ? ApiResponseFactory.Ok<object>(null, controller.HttpContext.TraceIdentifier)
            : ApiResponseFactory.Failure(result.Error, StatusCode(result.Error), controller.HttpContext.TraceIdentifier);

    private static int StatusCode(Error error) => error.Code switch
    {
        "PAYMENT.STUDENT_REQUIRED" or "PAYMENT.ADMIN_REQUIRED" or "PAYMENT.COURSE_FORBIDDEN" => ApiResponseCodes.Forbidden,
        "PAYMENT.BILL_NOT_FOUND" or "PAYMENT.PLAN_NOT_FOUND" or "PAYMENT.CHECKOUT_NOT_FOUND" or "PAYMENT.COURSE_NOT_FOUND" or "PAYMENT.NOT_FOUND" or "PAYMENT.ENROLLMENT_NOT_FOUND" => ApiResponseCodes.NotFound,
        "PAYMENT.CHECKOUT_CONFLICT" or "PAYMENT.STATEMENT_PAYMENT_IN_PROGRESS" or "PAYMENT.REFUND_EXCEEDS_PAYMENT" or "PAYMENT.INSUFFICIENT_BALANCE" => ApiResponseCodes.Conflict,
        "PAYMENT.PROVIDER_UNAVAILABLE" => ApiResponseCodes.BadGateway,
        _ => ApiResponseCodes.BadRequest
    };
}
