using Moe.SharedKernel.Domain;

namespace Moe.Modules.EducationAccountTopUp.Domain.TopUps;

public sealed class TopUpCampaignRule : Entity<long>
{
    private TopUpCampaignRule() : base(0) { }

    public long TopUpCampaignId { get; private set; }
    public long TopUpRuleGroupId { get; private set; }
    public int DisplayOrder { get; private set; }
    public string CriterionCode { get; private set; } = string.Empty;
    public string OperatorCode { get; private set; } = string.Empty;
    public decimal? NumericValueFrom { get; private set; }
    public decimal? NumericValueTo { get; private set; }
    public string? TextValue { get; private set; }

    public static TopUpCampaignRule Create(
        long topUpCampaignId,
        long topUpRuleGroupId,
        int displayOrder,
        string criterionCode,
        string operatorCode,
        decimal? numericValueFrom,
        decimal? numericValueTo,
        string? textValue)
    {
        if (topUpCampaignId <= 0) throw new ArgumentOutOfRangeException(nameof(topUpCampaignId));
        if (topUpRuleGroupId <= 0) throw new ArgumentOutOfRangeException(nameof(topUpRuleGroupId));
        if (displayOrder <= 0) throw new ArgumentOutOfRangeException(nameof(displayOrder));
        if (string.IsNullOrWhiteSpace(criterionCode)) throw new ArgumentException("Criterion code is required.", nameof(criterionCode));
        if (string.IsNullOrWhiteSpace(operatorCode)) throw new ArgumentException("Operator code is required.", nameof(operatorCode));

        return new TopUpCampaignRule
        {
            TopUpCampaignId = topUpCampaignId,
            TopUpRuleGroupId = topUpRuleGroupId,
            DisplayOrder = displayOrder,
            CriterionCode = criterionCode,
            OperatorCode = operatorCode,
            NumericValueFrom = numericValueFrom,
            NumericValueTo = numericValueTo,
            TextValue = textValue
        };
    }

    internal static TopUpCampaignRule CreateForNewGroup(
        long topUpCampaignId,
        int displayOrder,
        string criterionCode,
        string operatorCode,
        decimal? numericValueFrom,
        decimal? numericValueTo,
        string? textValue)
    {
        if (topUpCampaignId <= 0) throw new ArgumentOutOfRangeException(nameof(topUpCampaignId));
        if (displayOrder <= 0) throw new ArgumentOutOfRangeException(nameof(displayOrder));
        if (string.IsNullOrWhiteSpace(criterionCode)) throw new ArgumentException("Criterion code is required.", nameof(criterionCode));
        if (string.IsNullOrWhiteSpace(operatorCode)) throw new ArgumentException("Operator code is required.", nameof(operatorCode));

        return new TopUpCampaignRule
        {
            TopUpCampaignId = topUpCampaignId,
            DisplayOrder = displayOrder,
            CriterionCode = criterionCode,
            OperatorCode = operatorCode,
            NumericValueFrom = numericValueFrom,
            NumericValueTo = numericValueTo,
            TextValue = textValue
        };
    }
}
