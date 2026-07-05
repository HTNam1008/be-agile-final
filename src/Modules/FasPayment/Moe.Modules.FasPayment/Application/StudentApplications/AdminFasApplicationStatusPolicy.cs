using Moe.Modules.FasPayment.Domain.Fas;

namespace Moe.Modules.FasPayment.Application.StudentApplications;

internal static class AdminFasApplicationStatusPolicy
{
    public static bool TryGetReviewStatus(string applicationStatus, string selectionStatus, out string? status)
    {
        status = NormalizeReviewStatus(applicationStatus, selectionStatus);
        return status is not null;
    }

    public static bool MatchesReviewStatus(string reviewStatus, string? requestedStatus)
    {
        string normalized = requestedStatus?.Trim().ToUpperInvariant() ?? "ALL";
        return string.IsNullOrWhiteSpace(normalized) ||
               normalized == "ALL" ||
               string.Equals(reviewStatus, NormalizeRequestedStatus(normalized), StringComparison.OrdinalIgnoreCase);
    }

    private static string? NormalizeReviewStatus(string applicationStatus, string selectionStatus)
    {
        if (string.Equals(selectionStatus, "PENDING", StringComparison.OrdinalIgnoreCase) &&
            (string.Equals(applicationStatus, FasApplicationStatuses.Submitted, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(applicationStatus, FasApplicationStatuses.PendingReview, StringComparison.OrdinalIgnoreCase)))
        {
            return "PENDING";
        }

        if (string.Equals(selectionStatus, "APPROVED", StringComparison.OrdinalIgnoreCase))
        {
            return "APPROVED";
        }

        return string.Equals(selectionStatus, "REJECTED", StringComparison.OrdinalIgnoreCase)
            ? "REJECTED"
            : null;
    }

    private static string NormalizeRequestedStatus(string status) =>
        status.Replace("PENDING_FOR_REVIEW", "PENDING", StringComparison.OrdinalIgnoreCase)
            .Replace(FasApplicationStatuses.Submitted, "PENDING", StringComparison.OrdinalIgnoreCase)
            .Replace(FasApplicationStatuses.PendingReview, "PENDING", StringComparison.OrdinalIgnoreCase);
}
