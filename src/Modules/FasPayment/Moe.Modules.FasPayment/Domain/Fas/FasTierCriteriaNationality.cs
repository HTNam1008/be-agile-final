namespace Moe.Modules.FasPayment.Domain.Fas;

internal sealed class FasTierCriteriaNationality
{
    private FasTierCriteriaNationality() { }
    public long FasTierCriteriaId { get; private set; }
    public string Nationality { get; private set; } = string.Empty;
    public static FasTierCriteriaNationality Create(long criteriaId, string criteriaType, string value)
    {
        if (criteriaId <= 0) throw new ArgumentOutOfRangeException(nameof(criteriaId));
        string type = criteriaType?.Trim().ToUpperInvariant() ?? string.Empty;
        string normalized = value?.Trim() ?? string.Empty;
        bool supported = type switch
        {
            "NATIONALITY" or "PARENT_NATIONALITY" => FasNationalities.All.Contains(normalized, StringComparer.Ordinal),
            "ACCOUNT_TYPE" => normalized is "EDUCATION_ACCOUNT" or "PERSONAL_ACCOUNT",
            _ => false
        };
        if (!supported) throw new ArgumentException($"Unsupported value for {type}.", nameof(value));
        return new FasTierCriteriaNationality { FasTierCriteriaId = criteriaId, Nationality = normalized };
    }
}

internal static class FasNationalities
{
    public static readonly string[] All =
    [
        "Singapore Citizen", "Permanent Resident", "Foreigner"
    ];
}
