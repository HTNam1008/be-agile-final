namespace Moe.Modules.FasPayment.Domain.Fas;

internal sealed class FasTierCriteriaNationality
{
    private FasTierCriteriaNationality() { }
    public long FasTierCriteriaId { get; private set; }
    public string Nationality { get; private set; } = string.Empty;
    public static FasTierCriteriaNationality Create(long criteriaId, string nationality)
    {
        if (criteriaId <= 0) throw new ArgumentOutOfRangeException(nameof(criteriaId));
        string normalized = nationality?.Trim() ?? string.Empty;
        if (!FasNationalities.All.Contains(normalized, StringComparer.Ordinal)) throw new ArgumentException("Unsupported nationality.", nameof(nationality));
        return new FasTierCriteriaNationality { FasTierCriteriaId = criteriaId, Nationality = normalized };
    }
}

internal static class FasNationalities
{
    public static readonly string[] All =
    [
        "Singapore Citizen", "Permanent Resident", "International Student", "Malaysian",
        "Bruneian", "Indonesian", "Thai", "Vietnamese", "Filipino", "Cambodian",
        "Laotian", "Myanmar national", "Chinese", "Indian", "Other nationality"
    ];
}
