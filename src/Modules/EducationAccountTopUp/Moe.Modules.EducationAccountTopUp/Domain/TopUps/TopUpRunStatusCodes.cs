namespace Moe.Modules.EducationAccountTopUp.Domain.TopUps;

public static class TopUpRunStatusCodes
{
    public const string Previewed = "PREVIEWED";
    public const string Processing = "PROCESSING";
    public const string Completed = "COMPLETED";
    public const string Partial = "PARTIAL";
    public const string Failed = "FAILED";
    public const string Cancelled = "CANCELLED";

    public static readonly IReadOnlySet<string> TerminalStatuses = new HashSet<string>
    {
        Completed,
        Partial,
        Failed,
        Cancelled
    };

    public static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> ValidTransitions =
        new Dictionary<string, IReadOnlySet<string>>
        {
            [Previewed] = new HashSet<string> { Processing, Cancelled },
            [Processing] = new HashSet<string> { Completed, Partial, Failed }
        };
}
