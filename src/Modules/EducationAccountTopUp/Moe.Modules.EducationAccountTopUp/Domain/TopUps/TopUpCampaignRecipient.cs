using Moe.SharedKernel.Domain;

namespace Moe.Modules.EducationAccountTopUp.Domain.TopUps;

internal sealed class TopUpCampaignRecipient : Entity<long>
{
    private TopUpCampaignRecipient() : base(0) { }

    public long TopUpCampaignId { get; private set; }
    public long EducationAccountId { get; private set; }
    public decimal? AmountOverride { get; private set; }
    public bool IsActive { get; private set; }
    public long AddedByLoginAccountId { get; private set; }
    public DateTime AddedAtUtc { get; private set; }
}
