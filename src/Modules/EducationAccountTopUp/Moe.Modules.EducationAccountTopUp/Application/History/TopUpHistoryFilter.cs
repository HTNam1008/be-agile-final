namespace Moe.Modules.EducationAccountTopUp.Application.History;

/// <summary>
/// Normalized filter shared by top-up history and future export queries.
/// DateFromUtc is inclusive; DateToUtc is exclusive.
/// </summary>
public sealed record TopUpHistoryFilter(
    DateTime? DateFromUtc,
    DateTime? DateToUtc,
    long? CampaignId,
    string? CampaignSearch,
    long? OrganizationId,
    string? TriggerType,
    string? Status,
    string? StudentOrAccountSearch,
    long? ActorId);
