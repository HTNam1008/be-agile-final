using Moe.SharedKernel.Results;

namespace Moe.Modules.FasPayment.Domain.Payments;

public static class PaymentDomainErrors
{
    public static readonly Error InvalidPaymentMethod = new("PAYMENT.INVALID_METHOD", "Choose Education Account or online payment.");
    public static readonly Error BillNotFound = new("PAYMENT.BILL_NOT_FOUND", "The bill was not found.");
    public static readonly Error BillAlreadySettled = new("PAYMENT.BILL_ALREADY_SETTLED", "The bill has already been paid.");
    public static readonly Error AccountNotFound = new("PAYMENT.ACCOUNT_NOT_FOUND", "No active Education Account was found for this student.");
    public static readonly Error InsufficientBalance = new("PAYMENT.INSUFFICIENT_BALANCE", "The Education Account balance is not enough to pay this bill.");
    public static readonly Error InvalidPaymentPlan = new(
        "PAYMENT.PLAN_INVALID",
        "The course payment plan is invalid.");

    public static readonly Error InvalidCheckout = new(
        "PAYMENT.CHECKOUT_INVALID",
        "The payment checkout request is invalid.");

    public static readonly Error AmountNotDivisibleByInstallments = new(
        "PAYMENT.INSTALLMENT_AMOUNT_INVALID",
        "The bill amount must divide evenly across the selected installments.");

    public static readonly Error PaymentPlanNotFound = new(
        "PAYMENT.PLAN_NOT_FOUND",
        "The course payment plan was not found.");

    public static readonly Error CheckoutNotFound = new(
        "PAYMENT.CHECKOUT_NOT_FOUND",
        "The payment checkout was not found.");

    public static readonly Error CheckoutConflict = new(
        "PAYMENT.CHECKOUT_CONFLICT",
        "Another checkout is already in progress for this bill.");

    public static readonly Error StatementPaymentInProgress = new(
        "PAYMENT.STATEMENT_PAYMENT_IN_PROGRESS",
        "A payment for this monthly statement is already in progress. Complete it or wait for the checkout to expire before retrying.");

    public static readonly Error ProviderUnavailable = new(
        "PAYMENT.PROVIDER_UNAVAILABLE",
        "The payment provider is temporarily unavailable.");

    public static readonly Error InvalidWebhook = new(
        "PAYMENT.WEBHOOK_INVALID",
        "The payment webhook could not be verified.");

    public static readonly Error PaymentNotFound = new("PAYMENT.NOT_FOUND", "The payment was not found.");
    public static readonly Error InvalidRefund = new("PAYMENT.REFUND_INVALID", "The refund request is invalid.");
    public static readonly Error RefundExceedsPayment = new("PAYMENT.REFUND_EXCEEDS_PAYMENT", "The refund exceeds the remaining refundable amount.");
    public static readonly Error InvalidDeferral = new(
        "PAYMENT.INVALID_DEFERRAL",
        "The selected bills cannot be deferred.");

    public static readonly Error FullPaymentCannotBeDeferred = new(
        "PAYMENT.FULL_PAYMENT_CANNOT_BE_DEFERRED",
        "Full payment bills cannot be deferred. Retry payment, change payment plan, or cancel enrollment.");

    public static readonly Error NoDeferrableBills = new(
        "PAYMENT.NO_DEFERRABLE_BILLS",
        "There are no monthly installment bills eligible for deferral.");

    public static readonly Error EducationAccountCanCoverDeferral = new(
        "PAYMENT.EDUCATION_ACCOUNT_CAN_COVER_PAYMENT",
        "Your available Education Account balance can cover the monthly installment. Pay with your Education Account instead of deferring it.");
}
