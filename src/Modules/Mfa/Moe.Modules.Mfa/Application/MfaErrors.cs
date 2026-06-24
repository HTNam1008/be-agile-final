using Moe.SharedKernel.Results;

namespace Moe.Modules.Mfa.Application;

internal static class MfaErrors
{
    public static readonly Error AuthenticatedUserRequired = new(
        "MFA.AUTHENTICATED_USER_REQUIRED",
        "An authenticated user is required.");

    public static readonly Error ChallengeNotFound = new(
        "MFA.CHALLENGE_NOT_FOUND",
        "The MFA challenge was not found.");

    public static readonly Error ChallengeExpired = new(
        "MFA.CHALLENGE_EXPIRED",
        "The MFA challenge has expired.");

    public static readonly Error ChallengeNotPending = new(
        "MFA.CHALLENGE_NOT_PENDING",
        "The MFA challenge is no longer pending.");

    public static readonly Error InvalidPinFormat = new(
        "MFA.INVALID_PIN_FORMAT",
        "The PIN must contain exactly 4 digits.");

    public static readonly Error PinAlreadyConfigured = new(
        "MFA.PIN_ALREADY_CONFIGURED",
        "An MFA PIN is already configured for this account.");

    public static readonly Error PinNotConfigured = new(
        "MFA.PIN_NOT_CONFIGURED",
        "An MFA PIN has not been configured for this account.");

    public static readonly Error InvalidPin = new(
        "MFA.INVALID_PIN",
        "The MFA PIN is incorrect.");

    public static readonly Error CredentialLocked = new(
        "MFA.CREDENTIAL_LOCKED",
        "The MFA credential is temporarily locked.");

    public static readonly Error InvalidLoginAccount = new(
        "MFA.INVALID_LOGIN_ACCOUNT",
        "A valid login account is required.");

    public static readonly Error ResetReasonRequired = new(
        "MFA.RESET_REASON_REQUIRED",
        "A reset reason is required.");
}
