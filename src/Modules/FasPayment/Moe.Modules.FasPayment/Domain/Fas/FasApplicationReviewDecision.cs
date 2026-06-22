using Moe.SharedKernel.Domain;

namespace Moe.Modules.FasPayment.Domain.Fas;

internal sealed class FasApplicationReviewDecision : Entity<long>
{
    private FasApplicationReviewDecision() : base(0) { }
    public long FasApplicationId { get; private set; }
    public string Decision { get; private set; } = string.Empty;
    public long ReviewerLoginAccountId { get; private set; }
    public DateTime ReviewedAtUtc { get; private set; }
    public string? RejectionReasonCode { get; private set; }
    public string? Remarks { get; private set; }
    public static FasApplicationReviewDecision CreateApproval(long applicationId, long reviewerId, string? remarks, DateTime utcNow)
        => Create(applicationId, reviewerId, "APPROVED", null, remarks, utcNow);
    public static FasApplicationReviewDecision CreateRejection(long applicationId, long reviewerId, string rejectionReasonCode, string? remarks, DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(rejectionReasonCode)) throw new ArgumentException("Rejection reason is required.", nameof(rejectionReasonCode));
        return Create(applicationId, reviewerId, "REJECTED", rejectionReasonCode.Trim(), remarks, utcNow);
    }
    private static FasApplicationReviewDecision Create(long applicationId, long reviewerId, string decision, string? reason, string? remarks, DateTime utcNow)
    {
        if (applicationId <= 0) throw new ArgumentOutOfRangeException(nameof(applicationId));
        if (reviewerId <= 0) throw new ArgumentOutOfRangeException(nameof(reviewerId));
        return new FasApplicationReviewDecision { FasApplicationId = applicationId, ReviewerLoginAccountId = reviewerId, Decision = decision, RejectionReasonCode = reason, Remarks = string.IsNullOrWhiteSpace(remarks) ? null : remarks.Trim(), ReviewedAtUtc = utcNow };
    }
}
