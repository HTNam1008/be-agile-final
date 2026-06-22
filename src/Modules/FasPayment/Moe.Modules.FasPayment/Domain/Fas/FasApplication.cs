using System;
using Moe.SharedKernel.Domain;

namespace Moe.Modules.FasPayment.Domain.Fas;

internal sealed class FasApplication : Entity<long>
{
    private FasApplication() : base(0) { }

    public string ApplicationNo { get; private set; } = string.Empty;
    public long FasSchemeId { get; private set; }
    public string StudentId { get; private set; } = string.Empty;
    public string StudentName { get; private set; } = string.Empty;
    public DateOnly SubmittedDate { get; private set; }
    public string StatusCode { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    public static FasApplication Submit(
        string applicationNo,
        long fasSchemeId,
        string studentId,
        string studentName,
        DateOnly submittedDate)
    {
        return new FasApplication
        {
            ApplicationNo = applicationNo,
            FasSchemeId = fasSchemeId,
            StudentId = studentId,
            StudentName = studentName,
            SubmittedDate = submittedDate,
            StatusCode = FasApplicationStatuses.PendingReview,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Approve()
    {
        if (StatusCode != FasApplicationStatuses.PendingReview)
        {
            throw new DomainException($"Cannot approve application with status {StatusCode}. Must be {FasApplicationStatuses.PendingReview}.");
        }

        StatusCode = FasApplicationStatuses.Approved;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Reject()
    {
        if (StatusCode != FasApplicationStatuses.PendingReview)
        {
            throw new DomainException($"Cannot reject application with status {StatusCode}. Must be {FasApplicationStatuses.PendingReview}.");
        }

        StatusCode = FasApplicationStatuses.Rejected;
        UpdatedAt = DateTime.UtcNow;
    }
}
