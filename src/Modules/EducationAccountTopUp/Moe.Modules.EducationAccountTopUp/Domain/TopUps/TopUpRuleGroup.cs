using Moe.SharedKernel.Domain;

namespace Moe.Modules.EducationAccountTopUp.Domain.TopUps;

public sealed class TopUpRuleGroup : Entity<long>
{
    private TopUpRuleGroup() : base(0) { }

    public long TopUpCampaignId { get; private set; }
    public int DisplayOrder { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }

    public List<TopUpCampaignRule> Rules { get; private set; } = [];

    public void AddRule(
        int displayOrder,
        string criterionCode,
        string operatorCode,
        decimal? numericValueFrom,
        decimal? numericValueTo,
        string? textValue)
    {
        Rules.Add(TopUpCampaignRule.CreateForNewGroup(
            TopUpCampaignId,
            displayOrder,
            criterionCode,
            operatorCode,
            numericValueFrom,
            numericValueTo,
            textValue));
    }

    public static TopUpRuleGroup Create(long campaignId, int displayOrder, DateTime utcNow)
    {
        if (campaignId <= 0) throw new ArgumentOutOfRangeException(nameof(campaignId));
        if (displayOrder <= 0) throw new ArgumentOutOfRangeException(nameof(displayOrder));

        return new TopUpRuleGroup
        {
            TopUpCampaignId = campaignId,
            DisplayOrder = displayOrder,
            CreatedAtUtc = utcNow
        };
    }
}
