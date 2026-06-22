using Moe.SharedKernel.Domain;

namespace Moe.Modules.FasPayment.Domain.Fas;

internal sealed class FasRule : Entity<long>
{
    private FasRule() : base(0) { }

    public long FasTierId { get; private set; }
    public int RuleGroupNumber { get; private set; }
    public string CriterionCode { get; private set; } = string.Empty;
    public string OperatorCode { get; private set; } = string.Empty;
    public decimal? NumericValueFrom { get; private set; }
    public decimal? NumericValueTo { get; private set; }
    public string? TextValue { get; private set; }
    public int SequenceNumber { get; private set; }
    public bool IsActive { get; private set; }
}
