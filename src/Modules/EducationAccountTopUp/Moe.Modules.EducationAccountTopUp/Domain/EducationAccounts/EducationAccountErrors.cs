using Moe.SharedKernel.Results;

namespace Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;

public static class EducationAccountErrors
{
    public static readonly Error NotFound = new(
        "ACCOUNT.NOT_FOUND",
        "No education account was found for the current student.");

    public static readonly Error AuthenticatedStudentRequired = new(
        "ACCOUNT.AUTHENTICATED_STUDENT_REQUIRED",
        "An authenticated student is required.");

    public static readonly Error InvalidTransactionCategory = new(
        "ACCOUNT.INVALID_TRANSACTION_CATEGORY",
        "Transaction category must be TOP_UP, PAYMENT, REFUND, or REVERSAL.");
}
