namespace Moe.Modules.AiCopilot.Domain;

internal sealed class AiConversation
{
    private AiConversation() { }

    public Guid Id { get; private set; }
    public long PersonId { get; private set; }
    public string PortalCode { get; private set; } = "ESERVICE";
    public string ModeCode { get; private set; } = "GENERAL";
    public string StatusCode { get; private set; } = "ACTIVE";
    public string? PageContextJson { get; private set; }
    public AiFasSession? FasSession { get; internal set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }

    public static AiConversation Start(Guid id, long personId, DateTime now) => new()
    {
        Id = id,
        PersonId = personId,
        CreatedAtUtc = now,
        UpdatedAtUtc = now,
        ExpiresAtUtc = now.AddDays(30)
    };

    public void Touch(string mode, string? pageContextJson, DateTime now)
    {
        ModeCode = mode;
        PageContextJson = pageContextJson;
        UpdatedAtUtc = now;
        ExpiresAtUtc = now.AddDays(30);
    }
}

internal sealed class AiMessage
{
    private AiMessage() { }
    public long Id { get; private set; }
    public Guid ConversationId { get; private set; }
    public string RoleCode { get; private set; } = string.Empty;
    public string ContentRedacted { get; private set; } = string.Empty;
    public string? CitationsJson { get; private set; }
    public string? ToolSummaryJson { get; private set; }
    public string? ResponseJson { get; private set; }
    public int? LatencyMs { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public static AiMessage Create(Guid conversationId, string role, string content, DateTime now,
        string? citationsJson = null, string? toolSummaryJson = null, int? latencyMs = null, string? responseJson = null) => new()
        {
            ConversationId = conversationId,
            RoleCode = role,
            ContentRedacted = content,
            CitationsJson = citationsJson,
            ToolSummaryJson = toolSummaryJson,
            ResponseJson = responseJson,
            LatencyMs = latencyMs,
            CreatedAtUtc = now
        };
}

internal sealed class AiReviewRecord
{
    private AiReviewRecord() { }
    public Guid Id { get; private set; }
    public Guid ConversationId { get; private set; }
    public long PersonId { get; private set; }
    public string ReasonCode { get; private set; } = string.Empty;
    public string DomainCode { get; private set; } = string.Empty;
    public string SeverityCode { get; private set; } = "MEDIUM";
    public string StatusCode { get; private set; } = "OPEN";
    public string? Route { get; private set; }
    public string TranscriptRedacted { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? ResolvedAtUtc { get; private set; }
    public long? ResolvedByUserAccountId { get; private set; }

    public static AiReviewRecord Create(Guid conversationId, long personId, string reason, string domain,
        string? route, string transcript, DateTime now) => new()
        {
            Id = Guid.NewGuid(),
            ConversationId = conversationId,
            PersonId = personId,
            ReasonCode = reason,
            DomainCode = domain,
            Route = route,
            TranscriptRedacted = transcript,
            CreatedAtUtc = now
        };

    public void Resolve(long actorId, DateTime now)
    { StatusCode = "RESOLVED"; ResolvedByUserAccountId = actorId; ResolvedAtUtc = now; }
}

internal sealed class AdminCenterCase
{
    private AdminCenterCase() { }
    public Guid Id { get; private set; }
    public Guid ReviewRecordId { get; private set; }
    public long PersonId { get; private set; }
    public string DescriptionRedacted { get; private set; } = string.Empty;
    public string ContactPreferenceCode { get; private set; } = "PORTAL";
    public string StatusCode { get; private set; } = "OPEN";
    public DateTime CreatedAtUtc { get; private set; }

    public static AdminCenterCase Create(Guid reviewId, long personId, string description, string preference, DateTime now) => new()
    { Id = Guid.NewGuid(), ReviewRecordId = reviewId, PersonId = personId, DescriptionRedacted = description, ContactPreferenceCode = preference, CreatedAtUtc = now };
}
