using Moe.Application.Abstractions.Messaging;

namespace Moe.Modules.EducationAccountTopUp.Application.TopUps.DeleteCampaign;

public sealed record DeleteCampaignCommand(long TopUpCampaignId) : ICommand;
