namespace Moe.Modules.CourseBilling.Domain.Courses;

internal static class FeeComponentTypeCodes
{
    public const string Tuition = "TUITION";
    public const string Material = "MATERIAL";
    public const string Tax = "TAX";

    public static readonly string[] All =
    [
        Tuition,
        Material,
        Tax
    ];
}
