using Moe.SharedKernel.Domain;

namespace Moe.Modules.FasPayment.Domain.Fas;

internal sealed class FasTierCriteria : Entity<long>
{
    private FasTierCriteria() : base(0) { }
    public long FasTierId { get; private set; }
    public string CriteriaType { get; private set; } = string.Empty;
    public decimal? NumberFrom { get; private set; }
    public decimal? NumberTo { get; private set; }
    public string? ConnectorToNext { get; private set; }
    public int DisplayOrder { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }

    public static FasTierCriteria Create(long tierId, string criteriaType, decimal? numberFrom, decimal? numberTo,
        string? connectorToNext, int displayOrder, DateTime utcNow)
    {
        string type = criteriaType?.Trim().ToUpperInvariant() ?? string.Empty;
        string? connector = string.IsNullOrWhiteSpace(connectorToNext) ? null : connectorToNext.Trim().ToUpperInvariant();
        if (tierId <= 0) throw new ArgumentOutOfRangeException(nameof(tierId));
        if (type is not ("AGE" or "GDP" or "PCI" or "NATIONALITY")) throw new ArgumentException("Unsupported criteria type.", nameof(criteriaType));
        if (connector is not (null or "AND" or "OR")) throw new ArgumentException("Unsupported connector.", nameof(connectorToNext));
        if (displayOrder <= 0) throw new ArgumentOutOfRangeException(nameof(displayOrder));
        if (type == "NATIONALITY" && (numberFrom.HasValue || numberTo.HasValue)) throw new ArgumentException("Nationality criteria cannot have numeric bounds.");
        if (type != "NATIONALITY" && (!numberFrom.HasValue || !numberTo.HasValue || numberFrom > numberTo)) throw new ArgumentException("Numeric criteria require a valid inclusive range.");
        return new FasTierCriteria { FasTierId = tierId, CriteriaType = type, NumberFrom = numberFrom, NumberTo = numberTo, ConnectorToNext = connector, DisplayOrder = displayOrder, CreatedAtUtc = utcNow };
    }
}
