namespace Moe.Modules.Mfa.IGateway.Security;

internal interface IMfaPinHasher
{
    MfaPinHash Hash(string pin);

    bool Verify(string pin, byte[] salt, byte[] expectedHash);
}

internal sealed record MfaPinHash(byte[] Hash, byte[] Salt, string Algorithm);
