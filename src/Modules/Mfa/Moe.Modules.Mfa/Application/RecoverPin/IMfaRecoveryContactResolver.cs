namespace Moe.Modules.Mfa.Application.RecoverPin;

public interface IMfaRecoveryContactResolver
{
    Task<string?> ResolveEmailAsync(long loginAccountId, CancellationToken cancellationToken);
}
