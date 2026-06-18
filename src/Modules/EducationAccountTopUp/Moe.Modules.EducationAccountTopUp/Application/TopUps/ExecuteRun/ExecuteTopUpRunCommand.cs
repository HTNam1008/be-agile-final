using Moe.Application.Abstractions.Messaging;

namespace Moe.Modules.EducationAccountTopUp.Application.TopUps.ExecuteRun;

public sealed record ExecuteTopUpRunCommand(long TopUpCampaignId) : ICommand<long>;
