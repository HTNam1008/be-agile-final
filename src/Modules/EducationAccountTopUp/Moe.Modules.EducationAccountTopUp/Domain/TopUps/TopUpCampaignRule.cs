using Moe.SharedKernel.Domain;

namespace Moe.Modules.EducationAccountTopUp.Domain.TopUps;

internal sealed class TopUpCampaignRule : Entity<long>
{
    private TopUpCampaignRule() : base(0) { }

    public long TopUpCampaignId { get; private set; }
    public string CriterionCode { get; private set; } = string.Empty;
    public string OperatorCode { get; private set; } = string.Empty;
    public decimal? NumericValueFrom { get; private set; }
    public decimal? NumericValueTo { get; private set; }
    public string? TextValue { get; private set; }
    public bool IsActive { get; private set; }
}
