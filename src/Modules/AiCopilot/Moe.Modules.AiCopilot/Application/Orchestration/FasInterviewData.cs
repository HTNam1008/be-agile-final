using System.Text.Json;
using Moe.Modules.AiCopilot.Api;

namespace Moe.Modules.AiCopilot.Application.Orchestration;

public sealed class FasInterviewData
{
    public string Status { get; set; } = "COLLECTING";
    public JsonElement Profile { get; set; }
    public bool? IsWelfareHomeResident { get; set; }
    public string? Email { get; set; }
    public string? EmploymentStatusCode { get; set; }
    public decimal? MonthlyHouseholdIncome { get; set; }
    public int? HouseholdMemberCount { get; set; }
    public decimal? OtherMonthlyIncome { get; set; }
    public List<string> ParentNationalities { get; set; } = [];
    public List<FasApplicableSchemeOption> ApplicableSchemes { get; set; } = [];
    public List<string> ApplicableSchemeNames { get; set; } = [];
    public List<string> RequiredCriteriaTypes { get; set; } = [];
    public List<string> ProfileConfirmedFacts { get; set; } = [];
    public List<string> UserRequiredFacts { get; set; } = [];
    public List<FasRecommendationMatch> RecommendationMatches { get; set; } = [];
    public string? ClarificationField { get; set; }
    public string? ValidationMessage { get; set; }
    public string? PendingParentNationalitySuggestion { get; set; }
    public decimal? PendingIncomeConversion { get; set; }
    public Dictionary<string, int> ClarificationAttempts { get; set; } = [];
    public Dictionary<string, int> HelpAttempts { get; set; } = [];
}
