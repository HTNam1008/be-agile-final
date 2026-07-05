using System.ComponentModel.DataAnnotations;

namespace Moe.Modules.Mfa.Application;

public sealed class MfaOptions
{
    public const string SectionName = "Mfa";

    [Range(1, 60)]
    public int ChallengeLifetimeMinutes { get; init; } = 5;

    [Range(1, 20)]
    public int MaxFailedAttempts { get; init; } = 5;

    [Range(1, 1440)]
    public int LockoutMinutes { get; init; } = 5;

    [Range(10_000, 1_000_000)]
    public int Pbkdf2Iterations { get; init; } = 100_000;

    public string Pepper { get; init; } = string.Empty;

    [Range(5, 120)]
    public int RecoveryLinkLifetimeMinutes { get; init; } = 15;

    public string RecoveryPageUrl { get; init; } = "http://localhost:5173/mfa/reset";
}
