using Moe.Application.Abstractions.Messaging;

namespace Moe.Modules.EducationAccountTopUp.Application.TopUps.GetCampaigns;

public sealed record GetCampaignByIdQuery(long Id) : IQuery<CampaignListItem?>;
