using Moe.Modules.AiCopilot.Api;

namespace Moe.Modules.AiCopilot.Application.Orchestration;

public sealed record AiHandlerResult(
    string Text,
    string Mode,
    AiGrounding Grounding,
    IReadOnlyList<AiCard> Cards,
    IReadOnlyList<AiAction> Actions,
    AiInterviewState? InterviewState = null,
    Guid? ReviewRecordId = null)
{
    public IReadOnlyList<string> FollowUpQuestions { get; init; } = [];
    public string? TurnIntent { get; init; }
    public string? ConversationPhase { get; init; }
}