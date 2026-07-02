namespace Moe.Modules.IdentityPlatform.IGateway.Authentication;

internal interface IAdminLoginRecorder
{
    Task<bool> RecordSuccessfulLoginAsync(
        long userAccountId,
        DateTime utcNow,
        CancellationToken cancellationToken);
}
