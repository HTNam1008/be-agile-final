namespace Moe.Modules.IdentityPlatform.Domain.Schooling;

public static class SchoolLevelCodes
{
    public const string PostSecondary = "POST_SEC";
    public const string Bachelor = "BACHELOR";
    public const string Master = "MASTER";
    public const string Phd = "PHD";

    public static readonly IReadOnlyList<string> All =
    [
        PostSecondary,
        Bachelor,
        Master,
        Phd
    ];
}
