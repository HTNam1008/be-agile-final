using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Moe.Modules.Mfa.Application;
using Moe.Modules.Mfa.IGateway.Security;

namespace Moe.Modules.Mfa.Infrastructure;

internal sealed class Pbkdf2MfaPinHasher(IOptions<MfaOptions> options) : IMfaPinHasher
{
    private const int SaltSizeBytes = 32;
    private const int HashSizeBytes = 32;

    public MfaPinHash Hash(string pin)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(SaltSizeBytes);
        byte[] hash = HashWithSalt(pin, salt);

        return new MfaPinHash(
            hash,
            salt,
            $"PBKDF2-SHA256-{options.Value.Pbkdf2Iterations}");
    }

    public bool Verify(string pin, byte[] salt, byte[] expectedHash)
    {
        byte[] actualHash = HashWithSalt(pin, salt);
        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }

    private byte[] HashWithSalt(string pin, byte[] salt)
    {
        string secret = string.Concat(pin, options.Value.Pepper);

        return Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(secret),
            salt,
            options.Value.Pbkdf2Iterations,
            HashAlgorithmName.SHA256,
            HashSizeBytes);
    }
}
