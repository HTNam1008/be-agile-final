using System.Text.Json;

namespace Moe.Modules.AiCopilot.Domain;

public sealed class AiKnowledgeDocument
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private AiKnowledgeDocument() { }

    public string Id { get; internal set; } = string.Empty;
    public string Title { get; internal set; } = string.Empty;
    public string Section { get; internal set; } = string.Empty;
    public string Domain { get; internal set; } = "GENERAL";
    public string Status { get; internal set; } = "GUIDE";
    public string Version { get; internal set; } = "1.0";
    public DateOnly EffectiveDate { get; internal set; }
    public string Content { get; internal set; } = string.Empty;
    public string? Url { get; internal set; }
    public bool AlwaysInclude { get; internal set; }
    public string ReviewOwner { get; internal set; } = "Student Finance Product";
    public string? SynonymsJson { get; internal set; }
    public string? AllowedIntentsJson { get; internal set; }
    public string? FollowUpsJson { get; internal set; }
    public DateTime CreatedAtUtc { get; internal set; }
    public DateTime UpdatedAtUtc { get; internal set; }

    public string[]? Synonyms
    {
        get => SynonymsJson is null ? null : JsonSerializer.Deserialize<string[]>(SynonymsJson, JsonOptions);
        internal set => SynonymsJson = value is null ? null : JsonSerializer.Serialize(value, JsonOptions);
    }

    public string[]? AllowedIntents
    {
        get => AllowedIntentsJson is null ? null : JsonSerializer.Deserialize<string[]>(AllowedIntentsJson, JsonOptions);
        internal set => AllowedIntentsJson = value is null ? null : JsonSerializer.Serialize(value, JsonOptions);
    }

    public string[]? FollowUps
    {
        get => FollowUpsJson is null ? null : JsonSerializer.Deserialize<string[]>(FollowUpsJson, JsonOptions);
        internal set => FollowUpsJson = value is null ? null : JsonSerializer.Serialize(value, JsonOptions);
    }

    public static AiKnowledgeDocument Create(string id, string title, string domain, string content, DateTime now) => new()
    {
        Id = id,
        Title = title,
        Section = title,
        Domain = domain.ToUpperInvariant(),
        Content = content,
        Status = "GUIDE",
        Version = "1.0",
        EffectiveDate = DateOnly.FromDateTime(now),
        ReviewOwner = "Student Finance Product",
        CreatedAtUtc = now,
        UpdatedAtUtc = now,
    };
}
