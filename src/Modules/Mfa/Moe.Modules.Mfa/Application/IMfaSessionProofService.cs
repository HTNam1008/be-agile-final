namespace Moe.Modules.Mfa.Application;

public interface IMfaSessionProofService
{
    bool IsCurrentSessionVerified();
    void MarkCurrentSessionVerified();
}
