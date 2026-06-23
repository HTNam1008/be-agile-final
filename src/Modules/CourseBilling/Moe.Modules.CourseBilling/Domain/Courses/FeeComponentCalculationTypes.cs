namespace Moe.Modules.CourseBilling.Domain.Courses;

internal static class FeeComponentCalculationTypes
{
    public const string Fixed = "FIXED";
    public const string Percentage = "PERCENTAGE";

    public static readonly string[] All =
    [
        Fixed,
        Percentage
    ];
}
