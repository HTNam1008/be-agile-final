using Moe.SharedKernel.Results;

namespace Moe.Modules.FasPayment.Api.EService;

internal static class PaymentErrors
{
    public static readonly Error AuthenticatedStudentRequired = new(
        "PAYMENT.STUDENT_REQUIRED",
        "An authenticated student is required.");

    public static readonly Error BillNotFound = new(
        "PAYMENT.BILL_NOT_FOUND",
        "The bill was not found.");

    public static readonly Error BillAlreadySettled = new(
        "PAYMENT.BILL_ALREADY_SETTLED",
        "The bill has already been paid.");

    public static readonly Error InvalidPaymentMethod = new(
        "PAYMENT.INVALID_METHOD",
        "Choose Education Account or online payment.");

    public static readonly Error AccountNotFound = new(
        "PAYMENT.ACCOUNT_NOT_FOUND",
        "No active Education Account was found for this student.");

    public static readonly Error InsufficientBalance = new(
        "PAYMENT.INSUFFICIENT_BALANCE",
        "The Education Account balance is not enough to pay this bill.");
}
