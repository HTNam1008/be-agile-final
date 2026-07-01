using Moe.SharedKernel.Domain;

namespace Moe.Modules.FasPayment.Domain.Fas;

internal sealed class FasTierCriteria : Entity<long>
{
    private FasTierCriteria() : base(0) { }
    public long FasTierId { get; private set; }
    public long FasTierCriteriaGroupId { get; private set; }
    public string CriteriaType { get; private set; } = string.Empty;
    public decimal? NumberFrom { get; private set; }
    public decimal? NumberTo { get; private set; }
    public string? ConnectorToNext { get; private set; }
    public int DisplayOrder { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }

    public static FasTierCriteria Create(long tierId, long criteriaGroupId, string criteriaType, decimal? numberFrom, decimal? numberTo,
        string? connectorToNext, int displayOrder, DateTime utcNow, long entityId = 0)
    {
        string type = criteriaType?.Trim().ToUpperInvariant() ?? string.Empty;
        string? connector = string.IsNullOrWhiteSpace(connectorToNext) ? null : connectorToNext.Trim().ToUpperInvariant();
        if (tierId <= 0) throw new ArgumentOutOfRangeException(nameof(tierId));
        if (criteriaGroupId < 0) throw new ArgumentOutOfRangeException(nameof(criteriaGroupId));
        if (type is not ("AGE" or "GDP" or "GHI" or "PCI" or "NATIONALITY" or "PARENT_NATIONALITY" or "ACCOUNT_TYPE")) throw new ArgumentException("Unsupported criteria type.", nameof(criteriaType));
        if (connector is not (null or "AND" or "OR")) throw new ArgumentException("Unsupported connector.", nameof(connectorToNext));
        if (displayOrder <= 0) throw new ArgumentOutOfRangeException(nameof(displayOrder));
        bool categorical = type is "NATIONALITY" or "PARENT_NATIONALITY" or "ACCOUNT_TYPE";
        if (categorical && (numberFrom.HasValue || numberTo.HasValue)) throw new ArgumentException("Categorical criteria cannot have numeric bounds.");
        if (!categorical && (!numberFrom.HasValue || !numberTo.HasValue || numberFrom > numberTo)) throw new ArgumentException("Numeric criteria require a valid inclusive range.");
        return new FasTierCriteria { Id = entityId, FasTierId = tierId, FasTierCriteriaGroupId = criteriaGroupId, CriteriaType = type, NumberFrom = numberFrom, NumberTo = numberTo, ConnectorToNext = connector, DisplayOrder = displayOrder, CreatedAtUtc = utcNow };
    }

    public static FasTierCriteria Create(long tierId, string criteriaType, decimal? numberFrom, decimal? numberTo,
        string? connectorToNext, int displayOrder, DateTime utcNow, long entityId = 0)
        => Create(tierId, 0, criteriaType, numberFrom, numberTo, connectorToNext, displayOrder, utcNow, entityId);
}
