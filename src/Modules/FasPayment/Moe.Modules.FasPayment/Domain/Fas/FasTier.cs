using Moe.SharedKernel.Domain;

namespace Moe.Modules.FasPayment.Domain.Fas;

internal sealed class FasTier : Entity<long>
{
    private FasTier() : base(0) { }

    public long FasSchemeId { get; private set; }
    public string TierCode { get; private set; } = string.Empty;
    public string TierName { get; private set; } = string.Empty;
    public int PriorityNumber { get; private set; }
    public string StatusCode { get; private set; } = string.Empty;
}
