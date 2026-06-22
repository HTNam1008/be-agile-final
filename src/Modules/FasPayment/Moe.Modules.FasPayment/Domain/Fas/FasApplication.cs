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
            StatusCode = "PENDING_REVIEW",
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Approve()
    {
        if (StatusCode != "PENDING_REVIEW")
        {
            throw new DomainException($"Cannot approve application with status {StatusCode}. Must be PENDING_REVIEW.");
        }

        StatusCode = "APPROVED";
        UpdatedAt = DateTime.UtcNow;
    }

    public void Reject()
    {
        if (StatusCode != "PENDING_REVIEW")
        {
            throw new DomainException($"Cannot reject application with status {StatusCode}. Must be PENDING_REVIEW.");
        }

        StatusCode = "REJECTED";
        UpdatedAt = DateTime.UtcNow;
    }
}
