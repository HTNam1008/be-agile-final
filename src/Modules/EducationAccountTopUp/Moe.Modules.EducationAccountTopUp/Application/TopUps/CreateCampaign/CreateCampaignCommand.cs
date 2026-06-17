using Moe.Application.Abstractions.Messaging;
using Moe.Modules.EducationAccountTopUp.Contracts.TopUps.DTOs;

namespace Moe.Modules.EducationAccountTopUp.Application.TopUps.CreateCampaign;

public sealed record CreateCampaignCommand(CreateCampaignRequest Request) : ICommand<long>;
