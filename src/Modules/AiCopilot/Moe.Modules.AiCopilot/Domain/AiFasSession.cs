namespace Moe.Modules.AiCopilot.Domain;

public sealed class AiFasSession
{
    private AiFasSession() { }

    public Guid ConversationId { get; internal set; }
    public string StatusCode { get; internal set; } = "IDLE";
    public string? NextQuestion { get; internal set; }
    public int TurnCount { get; internal set; }
    public string? CollectedFactsJson { get; internal set; }
    public string? FormPatchJson { get; internal set; }
    public byte[] RowVersion { get; internal set; } = [];
    public DateTime UpdatedAtUtc { get; internal set; }

    public AiConversation Conversation { get; internal set; } = null!;

    public static AiFasSession Create(Guid conversationId, DateTime now) => new()
    {
        ConversationId = conversationId,
        StatusCode = "IDLE",
        UpdatedAtUtc = now
    };
}
