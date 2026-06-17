using Moe.Application.Abstractions.Messaging;

namespace Moe.Modules.EducationAccountTopUp.Application.RunExecution.RequestManualRun;

public sealed record RequestManualRunCommand(
    long CampaignId,
    string IdempotencyKey,
    string? Note) : ICommand<RequestManualRunResponse>;
