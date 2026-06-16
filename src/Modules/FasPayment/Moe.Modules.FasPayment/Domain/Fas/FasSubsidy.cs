using Moe.SharedKernel.Domain;

namespace Moe.Modules.FasPayment.Domain.Fas;

internal sealed class FasSubsidy : Entity<long>
{
    private FasSubsidy() : base(0) { }

    public long FasApplicationId { get; private set; }
    public long FasTierBenefitId { get; private set; }
    public long BillLineId { get; private set; }
    public decimal GrossAmountSnapshot { get; private set; }
    public decimal CalculatedAmount { get; private set; }
    public decimal AppliedAmount { get; private set; }
    public string SubsidyStatusCode { get; private set; } = string.Empty;
    public DateTime AppliedAtUtc { get; private set; }
}
