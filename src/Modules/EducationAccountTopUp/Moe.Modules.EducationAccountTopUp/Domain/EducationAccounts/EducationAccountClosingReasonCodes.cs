namespace Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;

public static class EducationAccountClosingReasonCodes
{
    public const string StudentIneligible = "STUDENT_INELIGIBLE";
    public const string DuplicateAccount = "DUPLICATE_ACCOUNT";
    public const string AdminError = "ADMIN_ERROR";
    public const string Other = "OTHER";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        StudentIneligible,
        DuplicateAccount,
        AdminError,
        Other
    };
}
