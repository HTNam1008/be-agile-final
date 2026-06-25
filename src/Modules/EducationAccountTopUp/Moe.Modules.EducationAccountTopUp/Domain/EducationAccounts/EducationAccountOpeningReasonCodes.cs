namespace Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;

public static class EducationAccountOpeningReasonCodes
{
    public const string AutoEligibility = "AUTO_ELIGIBILITY";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        AutoEligibility
    };
}
