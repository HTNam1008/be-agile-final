namespace Moe.Modules.EducationAccountTopUp.Domain.TopUps;

public static class TopUpTransactionStatusCodes
{
    public const string Pending = "PENDING";
    public const string Completed = "COMPLETED";
    public const string Failed = "FAILED";
    public const string Skipped = "SKIPPED";

    public static readonly IReadOnlySet<string> TerminalStatuses = new HashSet<string>
    {
        Completed,
        Failed,
        Skipped
    };
}
