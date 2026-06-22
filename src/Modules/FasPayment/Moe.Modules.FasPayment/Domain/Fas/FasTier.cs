using Moe.SharedKernel.Domain;

namespace Moe.Modules.FasPayment.Domain.Fas;

internal sealed class FasTier : Entity<long>
{
    private FasTier() : base(0) { }
    public long FasSchemeId { get; private set; }
    public string Label { get; private set; } = string.Empty;
    public string SubsidyType { get; private set; } = string.Empty;
    public decimal SubsidyValue { get; private set; }
    public int DisplayOrder { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }

    public static FasTier Create(long schemeId, string label, string subsidyType, decimal subsidyValue, int displayOrder, DateTime utcNow)
    {
        string type = subsidyType?.Trim().ToUpperInvariant() ?? string.Empty;
        if (schemeId <= 0) throw new ArgumentOutOfRangeException(nameof(schemeId));
        if (string.IsNullOrWhiteSpace(label)) throw new ArgumentException("Tier label is required.", nameof(label));
        if (type is not (FasSubsidyTypes.Fixed or FasSubsidyTypes.Percentage)) throw new ArgumentException("Unsupported subsidy type.", nameof(subsidyType));
        if (subsidyValue < 0 || type == FasSubsidyTypes.Percentage && subsidyValue > 100) throw new ArgumentOutOfRangeException(nameof(subsidyValue));
        if (displayOrder <= 0) throw new ArgumentOutOfRangeException(nameof(displayOrder));
        return new FasTier { FasSchemeId = schemeId, Label = label.Trim(), SubsidyType = type, SubsidyValue = subsidyValue, DisplayOrder = displayOrder, CreatedAtUtc = utcNow };
    }
}

internal static class FasSubsidyTypes
{
    public const string Fixed = "FIXED";
    public const string Percentage = "PERCENTAGE";
}
