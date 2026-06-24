namespace Moe.Modules.IdentityPlatform.Domain.Schooling;

public static class SchoolLevelCodes
{
    public const string Primary1 = "PRI_1";
    public const string Primary2 = "PRI_2";
    public const string Primary3 = "PRI_3";
    public const string Primary4 = "PRI_4";
    public const string Primary5 = "PRI_5";
    public const string Primary6 = "PRI_6";
    public const string Secondary1 = "SEC_1";
    public const string Secondary2 = "SEC_2";
    public const string Secondary3 = "SEC_3";
    public const string Secondary4 = "SEC_4";
    public const string Secondary5 = "SEC_5";

    public static readonly IReadOnlyList<string> All =
    [
        Primary1,
        Primary2,
        Primary3,
        Primary4,
        Primary5,
        Primary6,
        Secondary1,
        Secondary2,
        Secondary3,
        Secondary4,
        Secondary5
    ];
}
