namespace Moe.Modules.CourseBilling.Domain.Courses;

internal static class FeeComponentTypeCodes
{
    public const string Base = "BASE";
    public const string AddOn = "ADDON";
    public const string Tax = "TAX";

    public static readonly string[] All =
    [
        Base,
        AddOn,
        Tax
    ];
}
