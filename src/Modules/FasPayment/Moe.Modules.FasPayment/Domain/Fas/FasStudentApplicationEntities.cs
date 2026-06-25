using Moe.SharedKernel.Domain;

namespace Moe.Modules.FasPayment.Domain.Fas;

internal sealed class FasApplicationScheme : Entity<long>
{
    private FasApplicationScheme() : base(0) { }
    public long FasApplicationId { get; private set; }
    public long FasSchemeId { get; private set; }
    public string StatusCode { get; private set; } = "DRAFT";
    public string? RejectionNotes { get; private set; }
    public decimal? ApprovedAmount { get; private set; }
    public string? ApprovedComponentsJson { get; private set; }
    public DateTime? ApprovedAtUtc { get; private set; }
    public long? ApprovedByLoginAccountId { get; private set; }
    public DateTime? RejectedAtUtc { get; private set; }
    public long? RejectedByLoginAccountId { get; private set; }
    public DateOnly? ValidFrom { get; private set; }
    public DateOnly? ValidTo { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime? ActivatedAtUtc { get; private set; }
    public DateTime? RedeemedAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public long CreatedByLoginAccountId { get; private set; }
    public static FasApplicationScheme CreateDraft(long applicationId, long schemeId, long actorId, DateTime now) =>
        new() { FasApplicationId = applicationId, FasSchemeId = schemeId, CreatedByLoginAccountId = actorId, CreatedAtUtc = now };
    public void Submit() { if (StatusCode != "DRAFT") throw new DomainException("Only draft scheme selections can be submitted."); StatusCode = "PENDING"; }
    public void Approve(long actorId, decimal amount, string? components, DateOnly from, DateOnly to, DateTime now) { if (StatusCode != "PENDING" || to < from) throw new DomainException("Invalid approval transition."); StatusCode = "APPROVED"; ApprovedAmount = amount; ApprovedComponentsJson = components; ValidFrom = from; ValidTo = to; ApprovedByLoginAccountId = actorId; ApprovedAtUtc = now; }
    public void Reject(long actorId, string notes, DateTime now) { if (StatusCode != "PENDING" || string.IsNullOrWhiteSpace(notes)) throw new DomainException("Rejection notes are required."); StatusCode = "REJECTED"; RejectionNotes = notes.Trim(); RejectedByLoginAccountId = actorId; RejectedAtUtc = now; }
    public void Activate(DateTime now) { if (StatusCode != "APPROVED") throw new DomainException("Only approved schemes can be activated."); IsActive = true; ActivatedAtUtc = now; }
    public void Deactivate() => IsActive = false;
    public void Redeem(DateTime now)
    {
        if (StatusCode == "REDEEMED") return;
        if (StatusCode != "APPROVED") throw new DomainException("Only approved schemes can be redeemed.");
        StatusCode = "REDEEMED";
        IsActive = false;
        RedeemedAtUtc = now;
    }
}

internal sealed class FasDocument : Entity<long>
{
    private FasDocument() : base(0) { }
    public long FasApplicationId { get; private set; }
    public string DocumentTypeCode { get; private set; } = string.Empty;
    public string ChecklistItemCode { get; private set; } = string.Empty;
    public bool IsMandatory { get; private set; }
    public string FileName { get; private set; } = string.Empty;
    public string BlobKey { get; private set; } = string.Empty;
    public string MimeType { get; private set; } = string.Empty;
    public long FileSizeBytes { get; private set; }
    public string UploadStatusCode { get; private set; } = "UPLOADED";
    public long UploadedByLoginAccountId { get; private set; }
    public DateTime UploadedAtUtc { get; private set; }
    public DateTime? RemovedAtUtc { get; private set; }
    public long? RemovedByLoginAccountId { get; private set; }
    public long? ReplacedByDocumentId { get; private set; }
    public static FasDocument Create(long applicationId, string type, string item, bool mandatory, string fileName, string blobKey, string mime, long size, long actorId, DateTime now, bool scan) =>
        new() { FasApplicationId = applicationId, DocumentTypeCode = type, ChecklistItemCode = item, IsMandatory = mandatory, FileName = fileName, BlobKey = blobKey, MimeType = mime, FileSizeBytes = size, UploadedByLoginAccountId = actorId, UploadedAtUtc = now, UploadStatusCode = scan ? "SCAN_PENDING" : "UPLOADED" };
    public void Remove(long actorId, DateTime now) { UploadStatusCode = "REMOVED"; RemovedByLoginAccountId = actorId; RemovedAtUtc = now; }
    public void Replace(long replacementId, long actorId, DateTime now) { ReplacedByDocumentId = replacementId; Remove(actorId, now); }
    public void MarkScanPassed() { if (UploadStatusCode != "SCAN_PENDING") throw new DomainException("Only pending documents can pass scanning."); UploadStatusCode = "SCAN_PASSED"; }
    public void MarkScanFailed() { if (UploadStatusCode != "SCAN_PENDING") throw new DomainException("Only pending documents can fail scanning."); UploadStatusCode = "SCAN_FAILED"; }
}

internal sealed class FasDeclaration : Entity<long>
{
    private FasDeclaration() : base(0) { }
    public long FasApplicationId { get; private set; }
    public string DeclarationTypeCode { get; private set; } = string.Empty;
    public bool IsAccepted { get; private set; }
    public DateTime AcceptedAtUtc { get; private set; }
    public long AcceptedByLoginAccountId { get; private set; }
    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }
    public string DeclarationTextSnapshot { get; private set; } = string.Empty;
    public static FasDeclaration Accept(long applicationId, string type, string text, long actorId, DateTime now, string? ip, string? agent) { if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("Declaration text is required."); return new() { FasApplicationId = applicationId, DeclarationTypeCode = type, DeclarationTextSnapshot = text, IsAccepted = true, AcceptedByLoginAccountId = actorId, AcceptedAtUtc = now, IpAddress = ip, UserAgent = agent }; }
}

internal sealed class FasStatusHistory : Entity<long>
{
    private FasStatusHistory() : base(0) { }
    public long? FasApplicationId { get; private set; }
    public long? FasApplicationSchemeId { get; private set; }
    public string? OldStatusCode { get; private set; }
    public string NewStatusCode { get; private set; } = string.Empty;
    public string? Notes { get; private set; }
    public long ChangedByLoginAccountId { get; private set; }
    public string ChangedByRole { get; private set; } = string.Empty;
    public DateTime ChangedAtUtc { get; private set; }
    public static FasStatusHistory Create(long? appId, long? itemId, string? oldStatus, string newStatus, string? notes, long actorId, string role, DateTime now) => new() { FasApplicationId = appId, FasApplicationSchemeId = itemId, OldStatusCode = oldStatus, NewStatusCode = newStatus, Notes = notes, ChangedByLoginAccountId = actorId, ChangedByRole = role, ChangedAtUtc = now };
}

internal sealed class FasActiveScheme : Entity<long>
{
    private FasActiveScheme() : base(0) { }
    public long StudentPersonId { get; private set; }
    public long FasApplicationSchemeId { get; private set; }
    public long FasSchemeId { get; private set; }
    public DateOnly ActiveFrom { get; private set; }
    public DateOnly ActiveTo { get; private set; }
    public string StatusCode { get; private set; } = "ACTIVE";
    public DateTime ActivatedAtUtc { get; private set; }
    public long ActivatedByLoginAccountId { get; private set; }
    public DateTime? DeactivatedAtUtc { get; private set; }
    public long? DeactivatedByLoginAccountId { get; private set; }
    public string? DeactivatedReason { get; private set; }
    public static FasActiveScheme Activate(long studentId, long itemId, long schemeId, DateOnly from, DateOnly to, long actorId, DateTime now) { if (to < from) throw new ArgumentException("Active period is invalid."); return new() { StudentPersonId = studentId, FasApplicationSchemeId = itemId, FasSchemeId = schemeId, ActiveFrom = from, ActiveTo = to, ActivatedByLoginAccountId = actorId, ActivatedAtUtc = now }; }
    public void Deactivate(long actorId, DateTime now, string reason) { StatusCode = "DEACTIVATED"; DeactivatedByLoginAccountId = actorId; DeactivatedAtUtc = now; DeactivatedReason = reason; }
}

internal sealed class FasVoucherRedemption : Entity<long>
{
    private FasVoucherRedemption() : base(0) { }
    public long StudentPersonId { get; private set; }
    public long FasApplicationSchemeId { get; private set; }
    public long CourseId { get; private set; }
    public long CourseEnrollmentId { get; private set; }
    public long BillId { get; private set; }
    public decimal AppliedAmount { get; private set; }
    public string StatusCode { get; private set; } = "PENDING";
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? RedeemedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public static FasVoucherRedemption Pending(
        long studentPersonId,
        long fasApplicationSchemeId,
        long courseId,
        long courseEnrollmentId,
        long billId,
        decimal appliedAmount,
        DateTime utcNow)
    {
        if (studentPersonId <= 0 || fasApplicationSchemeId <= 0 || courseId <= 0 ||
            courseEnrollmentId <= 0 || billId <= 0)
        {
            throw new DomainException("FAS redemption target is invalid.");
        }

        return new()
        {
            StudentPersonId = studentPersonId,
            FasApplicationSchemeId = fasApplicationSchemeId,
            CourseId = courseId,
            CourseEnrollmentId = courseEnrollmentId,
            BillId = billId,
            AppliedAmount = decimal.Round(Math.Max(0m, appliedAmount), 2, MidpointRounding.AwayFromZero),
            CreatedAtUtc = utcNow
        };
    }

    public void Redeem(DateTime utcNow)
    {
        if (StatusCode == "REDEEMED") return;
        StatusCode = "REDEEMED";
        RedeemedAtUtc = utcNow;
    }

    public void Cancel()
    {
        if (StatusCode == "REDEEMED") return;
        StatusCode = "CANCELLED";
    }
}
