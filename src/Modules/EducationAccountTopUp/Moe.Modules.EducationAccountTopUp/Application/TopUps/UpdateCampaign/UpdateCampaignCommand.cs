using Moe.Application.Abstractions.Messaging;
using Moe.Modules.EducationAccountTopUp.Contracts.TopUps.DTOs;

namespace Moe.Modules.EducationAccountTopUp.Application.TopUps.UpdateCampaign;

public sealed record UpdateCampaignCommand(long TopUpCampaignId, UpdateCampaignRequest Request) : ICommand;
