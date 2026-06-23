using Moe.SharedKernel.Domain;

namespace Moe.Modules.FasPayment.Domain.Fas;

internal sealed class FasScheme : Entity<long>
{
    private FasScheme() : base(0) { }

    public string SchemeCode { get; private set; } = string.Empty;
    public string GrantCode { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public DateOnly StartDate { get; private set; }
    public DateOnly EndDate { get; private set; }
    public string StatusCode { get; private set; } = string.Empty;
    public long CreatedByLoginAccountId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public long? UpdatedByLoginAccountId { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }
    public long? ActivatedByLoginAccountId { get; private set; }
    public DateTime? ActivatedAtUtc { get; private set; }

    public static FasScheme CreateDraft(string schemeCode, string grantCode, string name,
        string? description, DateOnly startDate, DateOnly endDate, long actorId, DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(schemeCode)) throw new ArgumentException("Scheme code is required.", nameof(schemeCode));
        if (string.IsNullOrWhiteSpace(grantCode)) throw new ArgumentException("Grant code is required.", nameof(grantCode));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name is required.", nameof(name));
        if (endDate < startDate) throw new ArgumentException("End date must not be before start date.", nameof(endDate));

        return new FasScheme
        {
            SchemeCode = schemeCode.Trim(), GrantCode = grantCode.Trim(), Name = name.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            StartDate = startDate, EndDate = endDate, StatusCode = FasSchemeStatusCodes.Draft,
            CreatedByLoginAccountId = actorId, CreatedAtUtc = utcNow
        };
    }

    public void Activate(long actorId, DateTime utcNow)
    {
        if (StatusCode != FasSchemeStatusCodes.Draft) throw new InvalidOperationException("Only a draft scheme can be activated.");
        StatusCode = FasSchemeStatusCodes.Active;
        ActivatedByLoginAccountId = actorId;
        ActivatedAtUtc = utcNow;
        UpdatedByLoginAccountId = actorId;
        UpdatedAtUtc = utcNow;
    }

    public void UpdateDraft(string schemeCode, string grantCode, string name, string? description,
        DateOnly startDate, DateOnly endDate, long actorId, DateTime utcNow)
    {
        if (StatusCode != FasSchemeStatusCodes.Draft) throw new InvalidOperationException("Only a draft scheme can be updated.");
        if (string.IsNullOrWhiteSpace(schemeCode)) throw new ArgumentException("Scheme code is required.", nameof(schemeCode));
        if (string.IsNullOrWhiteSpace(grantCode)) throw new ArgumentException("Grant code is required.", nameof(grantCode));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name is required.", nameof(name));
        if (endDate < startDate) throw new ArgumentException("End date must not be before start date.", nameof(endDate));
        SchemeCode = schemeCode.Trim();
        GrantCode = grantCode.Trim();
        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        StartDate = startDate;
        EndDate = endDate;
        UpdatedByLoginAccountId = actorId;
        UpdatedAtUtc = utcNow;
    }

    public void Retire(long actorId, DateTime utcNow)
    {
        if (StatusCode != FasSchemeStatusCodes.Active) throw new InvalidOperationException("Only an active scheme can be retired.");
        StatusCode = FasSchemeStatusCodes.Retired;
        UpdatedByLoginAccountId = actorId;
        UpdatedAtUtc = utcNow;
    }

    public void Disable(long actorId, DateTime utcNow)
    {
        if (StatusCode != FasSchemeStatusCodes.Active) throw new InvalidOperationException("Only an active scheme can be disabled.");
        StatusCode = FasSchemeStatusCodes.Disabled;
        UpdatedByLoginAccountId = actorId;
        UpdatedAtUtc = utcNow;
    }

    public void Delete(long actorId, DateTime utcNow)
    {
        if (StatusCode == FasSchemeStatusCodes.Deleted) throw new InvalidOperationException("The scheme is already deleted.");
        StatusCode = FasSchemeStatusCodes.Deleted;
        UpdatedByLoginAccountId = actorId;
        UpdatedAtUtc = utcNow;
    }
}

internal static class FasSchemeStatusCodes
{
    public const string Draft = "DRAFT";
    public const string Active = "ACTIVE";
    public const string Retired = "RETIRED";
    public const string Disabled = "DISABLED";
    public const string Deleted = "DELETED";
}
