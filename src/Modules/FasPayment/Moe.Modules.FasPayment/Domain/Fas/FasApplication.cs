using Moe.SharedKernel.Domain;

namespace Moe.Modules.FasPayment.Domain.Fas;

internal sealed class FasApplication : Entity<long>
{
    private FasApplication() : base(0) { }

    public string ApplicationNo { get; private set; } = string.Empty;
    public long FasSchemeId { get; private set; }
    public long AccountHolderPersonId { get; private set; }
    public long StudentPersonId { get; private set; }
    public string StudentId { get; private set; } = string.Empty;
    public string StudentName { get; private set; } = string.Empty;
    public string? NricFinMasked { get; private set; }
    public DateOnly? DateOfBirth { get; private set; }
    public string? NationalityCode { get; private set; }
    public string? ParentNationalitiesJson { get; private set; }
    public string AccountTypeCode { get; private set; } = "PERSONAL_ACCOUNT";
    public string? Mobile { get; private set; }
    public string? Address { get; private set; }
    public string? Email { get; private set; }
    public long? SchoolOrganizationId { get; private set; }
    public string? SchoolName { get; private set; }
    public string? StudentNumber { get; private set; }
    public bool? IsWelfareHomeResident { get; private set; }
    public string? EmploymentStatusCode { get; private set; }
    public decimal? MonthlyHouseholdIncome { get; private set; }
    public int? HouseholdMemberCount { get; private set; }
    public decimal? OtherMonthlyIncome { get; private set; }
    public decimal? PerCapitaIncome { get; private set; }
    public DateOnly SubmittedDate { get; private set; }
    public DateTime? SubmittedAtUtc { get; private set; }
    public DateTime? LockedAtUtc { get; private set; }
    public string StatusCode { get; private set; } = FasApplicationStatuses.Draft;
    public DateTime CreatedAt { get; private set; }
    public long CreatedByLoginAccountId { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public long? UpdatedByLoginAccountId { get; private set; }

    public static FasApplication CreateDraft(string applicationNo, long personId, long schemeId,
        string studentNumber, string name, string? nric, DateOnly dob, string nationality,
        string? mobile, string? address, string? email, long schoolId, string schoolName, string accountTypeCode,
        long actorId, DateTime now)
    {
        if (personId <= 0 || actorId <= 0 || schemeId <= 0 || schoolId <= 0) throw new DomainException("Student, actor, scheme and current school are required.");
        if (string.IsNullOrWhiteSpace(applicationNo) || string.IsNullOrWhiteSpace(studentNumber) || string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(nric) || string.IsNullOrWhiteSpace(nationality) || string.IsNullOrWhiteSpace(mobile) || string.IsNullOrWhiteSpace(address) || string.IsNullOrWhiteSpace(schoolName)) throw new DomainException("The student profile is incomplete.");
        return new()
        {
            ApplicationNo = applicationNo,
            AccountHolderPersonId = personId,
            StudentPersonId = personId,
            FasSchemeId = schemeId,
            StudentId = studentNumber,
            StudentNumber = studentNumber,
            StudentName = name,
            NricFinMasked = nric,
            DateOfBirth = dob,
            NationalityCode = nationality,
            Mobile = mobile,
            Address = address,
            Email = email,
            SchoolOrganizationId = schoolId,
            AccountTypeCode = accountTypeCode is "EDUCATION_ACCOUNT" ? accountTypeCode : "PERSONAL_ACCOUNT",
            SchoolName = schoolName,
            StatusCode = FasApplicationStatuses.Draft,
            CreatedByLoginAccountId = actorId,
            CreatedAt = now,
            SubmittedDate = DateOnly.FromDateTime(now)
        };
    }

    public void ReplacePrimaryScheme(long schemeId, long actorId, DateTime now)
    { EnsureDraft(); FasSchemeId = schemeId; Touch(actorId, now); }

    public void UpdateEmail(string email, long actorId, DateTime now)
    { EnsureDraft(); if (string.IsNullOrWhiteSpace(email) || !System.Net.Mail.MailAddress.TryCreate(email, out _)) throw new DomainException("A valid email is required."); Email = email.Trim(); Touch(actorId, now); }

    public void UpdateParentNationalities(IEnumerable<string> nationalities, long actorId, DateTime now)
    {
        EnsureDraft();
        string[] values = nationalities.Select(x => x?.Trim()).Where(x => !string.IsNullOrWhiteSpace(x)).Cast<string>().Distinct(StringComparer.Ordinal).ToArray();
        if (values.Length == 0 || values.Any(x => !FasNationalities.All.Contains(x))) throw new DomainException("At least one supported parent nationality is required.");
        ParentNationalitiesJson = System.Text.Json.JsonSerializer.Serialize(values); Touch(actorId, now);
    }

    public void UpdateIncome(bool welfareHome, string? employmentStatus, decimal? ghi, int? members,
        decimal otherIncome, long actorId, DateTime now)
    {
        EnsureDraft();
        if (welfareHome) { IsWelfareHomeResident = true; EmploymentStatusCode = null; MonthlyHouseholdIncome = null; HouseholdMemberCount = null; OtherMonthlyIncome = null; PerCapitaIncome = null; Touch(actorId, now); return; }
        if (!ghi.HasValue || !members.HasValue || ghi < 0 || otherIncome < 0 || members <= 0) throw new DomainException("Income and household values are invalid.");
        var status = employmentStatus?.Trim().ToUpperInvariant(); if (status is not ("EMPLOYED" or "SELF_EMPLOYED" or "UNEMPLOYED")) throw new DomainException("Employment status must be EMPLOYED, SELF_EMPLOYED or UNEMPLOYED.");
        IsWelfareHomeResident = welfareHome; EmploymentStatusCode = status;
        MonthlyHouseholdIncome = ghi; HouseholdMemberCount = members; OtherMonthlyIncome = otherIncome;
        PerCapitaIncome = (ghi.Value + otherIncome) / members.Value; Touch(actorId, now);
    }

    public void SubmitDraft(long actorId, DateTime now)
    { EnsureDraft(); StatusCode = FasApplicationStatuses.Submitted; SubmittedAtUtc = now; SubmittedDate = DateOnly.FromDateTime(now); LockedAtUtc = now; Touch(actorId, now); }

    public void Withdraw(long actorId, DateTime now)
    {
        if (StatusCode != FasApplicationStatuses.Submitted && StatusCode != FasApplicationStatuses.PendingReview)
        {
            throw new DomainException("Only a pending submitted application can be withdrawn.");
        }

        StatusCode = FasApplicationStatuses.Withdrawn;
        Touch(actorId, now);
    }

    public void Approve() { if (StatusCode is not (FasApplicationStatuses.PendingReview or FasApplicationStatuses.Submitted)) throw new DomainException($"Cannot approve application with status {StatusCode}."); StatusCode = FasApplicationStatuses.Approved; UpdatedAt = DateTime.UtcNow; }
    public void Reject() { if (StatusCode is not (FasApplicationStatuses.PendingReview or FasApplicationStatuses.Submitted)) throw new DomainException($"Cannot reject application with status {StatusCode}."); StatusCode = FasApplicationStatuses.Rejected; UpdatedAt = DateTime.UtcNow; }
    private void EnsureDraft() { if (StatusCode != FasApplicationStatuses.Draft) throw new DomainException("Only a draft application can be changed."); }
    private void Touch(long actorId, DateTime now) { UpdatedByLoginAccountId = actorId; UpdatedAt = now; }
}
