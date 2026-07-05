using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Moe.Modules.AiCopilot.Application.Knowledge;
using Moe.Modules.AiCopilot.Application.Orchestration;

namespace Moe.Modules.AiCopilot.Api;

public sealed record AiPageContext(string? Domain, string? Surface, string? Path, JsonElement? Entity);
public sealed class AiChatRequest
{
    public Guid? ConversationId { get; init; }
    [Required, StringLength(4000, MinimumLength = 1)] public string Message { get; init; } = string.Empty;
    public AiPageContext? PageContext { get; init; }
    public FasInterviewData? FasState { get; init; }
}
public sealed record AiAction(string Type, string Label, string? Route = null, object? Payload = null);
public sealed record AiCard(string Type, object Data);
public sealed record AiGrounding(bool IsGrounded, IReadOnlyCollection<KnowledgeCitation> Citations);
public sealed record AiInterviewField(string Name, object? Value, string Provenance, bool Confirmed);
public sealed record FasRecommendationCard(
    decimal? PerCapitaIncome,
    string? RecommendedSchemeName,
    string? RecommendedTierLabel,
    string? SubsidyType,
    decimal? SubsidyValue,
    IReadOnlyCollection<FasRecommendationMatch> MatchedSchemes,
    IReadOnlyCollection<AiInterviewField> ConfirmedFacts,
    IReadOnlyCollection<string> MissingFacts,
    string Warning,
    string RecommendationConfidence = "MEDIUM",
    bool IsComparable = true,
    string? RankingSummary = null);
public sealed record FasRecommendationMatch(long SchemeId, string SchemeName, long TierId, string TierLabel, string SubsidyType, decimal SubsidyValue,
    int RecommendationRank = 0, string? RecommendationReason = null, string RecommendationConfidence = "MEDIUM", bool IsComparable = true,
    bool CanApply = true, bool HasPendingApplication = false, long? PendingApplicationId = null);
public sealed record KnowledgeAnswerCard(
    string Title,
    string Summary,
    IReadOnlyCollection<string> KeyFacts,
    IReadOnlyCollection<string> NextSteps,
    IReadOnlyCollection<string> SourceIds,
    string SourceQuality,
    IReadOnlyCollection<string> FollowUpQuestions,
    IReadOnlyCollection<KnowledgeSourceSummary> SourceSummaries,
    string KnowledgeVersion);
public sealed record FasPatchParticulars(string? Email, IReadOnlyCollection<string>? ParentNationalities);
public sealed record FasPatchIncome(
    bool? IsWelfareHomeResident, string? EmploymentStatusCode,
    decimal? MonthlyHouseholdIncome, int? HouseholdMemberCount, decimal? OtherMonthlyIncome);
public sealed record FasPatchSchemes(IReadOnlyCollection<long>? RecommendedSchemeIds, IReadOnlyCollection<string>? RecommendedSchemeNames);
public sealed record FasPatchMetaField(string Confidence, string Provenance, string? Explanation);
public sealed record FasFormPatch(
    FasPatchParticulars? Particulars, FasPatchIncome? Income, FasPatchSchemes? Schemes,
    IReadOnlyDictionary<string, FasPatchMetaField>? Meta);
public sealed record AiInterviewState(string Status, string? NextQuestion,
    IReadOnlyCollection<AiInterviewField> Fields, IReadOnlyCollection<string> MissingFields, object? FormPatch);
public sealed record AiChatResponse(Guid ConversationId, long MessageId, string Text, string Mode,
    AiGrounding Grounding, IReadOnlyCollection<AiCard> Cards, IReadOnlyCollection<AiAction> Actions,
    AiInterviewState? InterviewState, Guid? ReviewRecordId = null, FasInterviewData? FasState = null)
{
    public IReadOnlyCollection<string> FollowUpQuestions { get; init; } = [];
    public string? TurnIntent { get; init; }
    public string? ConversationPhase { get; init; }
}
public sealed record CreateAdminCenterCaseRequest(Guid ReviewRecordId,
    [Required, StringLength(2000, MinimumLength = 5)] string Description,
    [RegularExpression("PORTAL|EMAIL|PHONE")] string ContactPreference = "PORTAL");
