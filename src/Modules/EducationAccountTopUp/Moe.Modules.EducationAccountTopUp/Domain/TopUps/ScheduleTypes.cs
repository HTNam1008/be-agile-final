namespace Moe.Modules.EducationAccountTopUp.Domain.TopUps;

public static class ScheduleTypes
{
    public const string Immediate = "IMMEDIATE";
    public const string OneTimeScheduled = "ONETIME_SCHEDULED";
    public const string Recurring = "RECURRING";
    public const string Manual = "MANUAL";

    public static readonly string[] ValidTypes = { Immediate, OneTimeScheduled, Recurring, Manual };

    public static bool IsValid(string? type) =>
        !string.IsNullOrWhiteSpace(type) && ValidTypes.Contains(type.ToUpperInvariant());
}
