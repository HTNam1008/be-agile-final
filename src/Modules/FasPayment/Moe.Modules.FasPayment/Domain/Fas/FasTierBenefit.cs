using Moe.SharedKernel.Domain;

namespace Moe.Modules.FasPayment.Domain.Fas;

internal sealed class FasTierBenefit : Entity<long>
{
    private FasTierBenefit() : base(0) { }

    public long FasTierId { get; private set; }
    public long FeeComponentId { get; private set; }
    public string SubsidyTypeCode { get; private set; } = string.Empty;
    public decimal SubsidyValue { get; private set; }
    public decimal? MaximumSubsidyAmount { get; private set; }
    public bool IsActive { get; private set; }
}
