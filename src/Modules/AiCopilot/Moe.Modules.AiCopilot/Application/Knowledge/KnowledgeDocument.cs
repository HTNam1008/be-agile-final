namespace Moe.Modules.AiCopilot.Application.Knowledge;

public sealed record KnowledgeDocument(
    string Id, string Title, string Section, string Domain, string Status,
    string Version, DateOnly EffectiveDate, string Content, string? Url,
    string[]? Synonyms, bool AlwaysInclude, string ReviewOwner = "Student Finance Product",
    string[]? AllowedIntents = null, string[]? FollowUps = null);
