using Moe.Modules.EducationAccountTopUp.Domain.TopUps;

namespace Moe.Modules.EducationAccountTopUp.Application.RunExecution.TransactionResults;

internal static class TopUpSafeReasonPresenter
{
    private static readonly IReadOnlySet<string> SafeDisplayReasons = new HashSet<string>(
        [
            SafeReasons.AccountClosed,
            SafeReasons.AccountPendingClosure,
            SafeReasons.AccountNotActive,
            SafeReasons.RecipientNotEligible,
            SafeReasons.DuplicateRecipient,
            SafeReasons.CreditServiceUnavailable,
            SafeReasons.CreditRejected,
            SafeReasons.InvalidAmount,
            SafeReasons.NonPositiveAmount,
            SafeReasons.TransientErrorExhaustedRetries,
            SafeReasons.UnexpectedError
        ],
        StringComparer.Ordinal);

    public static string? Present(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return null;
        }

        string trimmed = reason.Trim();
        return SafeDisplayReasons.Contains(trimmed)
            ? trimmed
            : SafeReasons.UnexpectedError;
    }
}
