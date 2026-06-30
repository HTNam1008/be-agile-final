using Moe.SharedKernel.Domain;

namespace Moe.Modules.FasPayment.Domain.Fas;

internal sealed class FasTierCriteriaGroup : Entity<long>
{
    private FasTierCriteriaGroup() : base(0) { }

    public long FasTierId { get; private set; }
    public int DisplayOrder { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }

    public static FasTierCriteriaGroup Create(long tierId, int displayOrder, DateTime utcNow, long entityId = 0)
    {
        if (tierId <= 0) throw new ArgumentOutOfRangeException(nameof(tierId));
        if (displayOrder <= 0) throw new ArgumentOutOfRangeException(nameof(displayOrder));

        return new FasTierCriteriaGroup
        {
            Id = entityId,
            FasTierId = tierId,
            DisplayOrder = displayOrder,
            CreatedAtUtc = utcNow
        };
    }
}
