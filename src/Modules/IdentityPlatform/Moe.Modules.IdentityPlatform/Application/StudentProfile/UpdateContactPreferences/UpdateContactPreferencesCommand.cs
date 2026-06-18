using Moe.Application.Abstractions.Messaging;

namespace Moe.Modules.IdentityPlatform.Application.StudentProfile.UpdateContactPreferences;

public sealed record UpdateContactPreferencesCommand(
    string? PreferredEmail,
    string? PreferredMobile,
    string? PreferredAddress,
    DateTime? ExpectedUpdatedAtUtc) : ICommand<StudentProfileResponse>;
