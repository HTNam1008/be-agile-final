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

    public static TopUpCampaignRule Create(
        long topUpCampaignId,
        string criterionCode,
        string operatorCode,
        decimal? numericValueFrom,
        decimal? numericValueTo,
        string? textValue)
    {
        return new TopUpCampaignRule
        {
            TopUpCampaignId = topUpCampaignId,
            CriterionCode = criterionCode,
            OperatorCode = operatorCode,
            NumericValueFrom = numericValueFrom,
            NumericValueTo = numericValueTo,
            TextValue = textValue,
            IsActive = true
        };
    }
}
