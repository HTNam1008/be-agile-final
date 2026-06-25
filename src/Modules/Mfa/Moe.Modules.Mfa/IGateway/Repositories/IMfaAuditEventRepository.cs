namespace Moe.Modules.Mfa.IGateway.Repositories;

internal interface IMfaAuditEventRepository
{
    void Add(
        long loginAccountId,
        Guid? challengeId,
        string eventCode,
        long? performedByAccountId = null,
        string? reason = null);
}
