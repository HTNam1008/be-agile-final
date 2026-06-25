using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Moe.Modules.AiCopilot.Application.Knowledge;

namespace Moe.Modules.AiCopilot.Api;

public sealed record AiPageContext(string? Domain, string? Surface, string? Path, JsonElement? Entity);
public sealed class AiChatRequest
{
    public Guid? ConversationId { get; init; }
    [Required, StringLength(4000, MinimumLength = 1)] public string Message { get; init; } = string.Empty;
    public AiPageContext? PageContext { get; init; }
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
    string Warning);
public sealed record FasRecommendationMatch(long SchemeId, string SchemeName, long TierId, string TierLabel, string SubsidyType, decimal SubsidyValue);
public sealed record FasFormPatch(
    bool? IsWelfareHomeResident,
    decimal? MonthlyHouseholdIncome,
    int? HouseholdMemberCount,
    decimal OtherMonthlyIncome,
    IReadOnlyCollection<string> ParentNationalities,
    string EmploymentStatusCode = "EMPLOYED");
public sealed record AiInterviewState(string Status, string? NextQuestion,
    IReadOnlyCollection<AiInterviewField> Fields, IReadOnlyCollection<string> MissingFields, object? FormPatch);
public sealed record AiChatResponse(Guid ConversationId, long MessageId, string Text, string Mode,
    AiGrounding Grounding, IReadOnlyCollection<AiCard> Cards, IReadOnlyCollection<AiAction> Actions,
    AiInterviewState? InterviewState, Guid? ReviewRecordId = null);
public sealed record AiConversationResponse(Guid ConversationId, string Mode, string Status,
    IReadOnlyCollection<AiConversationMessageResponse> Messages, AiInterviewState? InterviewState);
public sealed record AiConversationMessageResponse(long MessageId, string Role, string Content, DateTime CreatedAtUtc);
public sealed record CreateAdminCenterCaseRequest(Guid ReviewRecordId,
    [property: Required, StringLength(2000, MinimumLength = 5)] string Description,
    [property: RegularExpression("PORTAL|EMAIL|PHONE")] string ContactPreference = "PORTAL");
