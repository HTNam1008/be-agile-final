//using System;
//using Moe.SharedKernel.Domain;

//namespace Moe.Modules.FasPayment.Domain.Fas;

//// STUB for Dev 1 entity to allow compilation
//internal sealed class FasTierCriteria : Entity<long>
//{
//    public FasTierCriteria(long id) : base(id) { }

//    public string CriteriaType { get; set; } = string.Empty;
//    public decimal? NumberFrom { get; set; }
//    public decimal? NumberTo { get; set; }
//    public string? ConnectorToNext { get; set; }
//}

//// STUB for Dev 1 entity
//internal sealed class FasApplicationReviewDecision : Entity<long>
//{
//    private FasApplicationReviewDecision() : base(0) { }

//    public long ApplicationId { get; private set; }
//    public string Decision { get; private set; } = string.Empty;
//    public string ReviewerUserId { get; private set; } = string.Empty;
//    public DateTime ReviewedAt { get; private set; }
//    public string? RejectionReasonCode { get; private set; }
//    public string? Remarks { get; private set; }

//    public static FasApplicationReviewDecision CreateApproval(long applicationId, string reviewerUserId, string? remarks)
//    {
//        return new FasApplicationReviewDecision
//        {
//            ApplicationId = applicationId,
//            Decision = "APPROVED",
//            ReviewerUserId = reviewerUserId,
//            ReviewedAt = DateTime.UtcNow,
//            Remarks = remarks
//        };
//    }

//    public static FasApplicationReviewDecision CreateRejection(long applicationId, string reviewerUserId, string rejectionReasonCode, string? remarks)
//    {
//        if (string.IsNullOrWhiteSpace(rejectionReasonCode))
//        {
//            throw new ArgumentException("Rejection reason code is mandatory.", nameof(rejectionReasonCode));
//        }

//        return new FasApplicationReviewDecision
//        {
//            ApplicationId = applicationId,
//            Decision = "REJECTED",
//            ReviewerUserId = reviewerUserId,
//            ReviewedAt = DateTime.UtcNow,
//            RejectionReasonCode = rejectionReasonCode,
//            Remarks = remarks
//        };
//    }
//}
