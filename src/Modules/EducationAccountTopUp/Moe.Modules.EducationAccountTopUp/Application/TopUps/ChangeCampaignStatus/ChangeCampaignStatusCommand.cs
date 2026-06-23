using Moe.Application.Abstractions.Messaging;

namespace Moe.Modules.EducationAccountTopUp.Application.TopUps.ChangeCampaignStatus;

public sealed record ChangeCampaignStatusCommand(long TopUpCampaignId, string NewStatusCode) : ICommand;
