namespace Moe.Modules.IdentityPlatform.Application.Students.CreateStudent;

internal static class SingaporeIdentityNumberValidator
{
    private static readonly int[] Weights = [2, 7, 6, 5, 4, 3, 2];

    public static bool IsValid(string? identityNumber)
    {
        if (string.IsNullOrWhiteSpace(identityNumber))
        {
            return false;
        }

        string normalized = identityNumber.Trim().ToUpperInvariant();
        if (normalized.Length != 9)
        {
            return false;
        }

        char prefix = normalized[0];
        if (prefix is not ('S' or 'T' or 'F' or 'G' or 'M'))
        {
            return false;
        }

        int sum = prefix is 'T' or 'G' ? 4 : prefix is 'M' ? 3 : 0;
        for (int index = 0; index < Weights.Length; index++)
        {
            char digit = normalized[index + 1];
            if (!char.IsAsciiDigit(digit))
            {
                return false;
            }

            sum += (digit - '0') * Weights[index];
        }

        char suffix = normalized[8];
        if (!char.IsAsciiLetterUpper(suffix))
        {
            return false;
        }

        string checksumTable = prefix switch
        {
            'S' or 'T' => "JZIHGFEDCBA",
            'F' or 'G' => "XWUTRQPNMLK",
            'M' => "XWUTRQPNJLK",
            _ => throw new InvalidOperationException("Unsupported NRIC/FIN prefix.")
        };

        return suffix == checksumTable[sum % 11];
    }
}
