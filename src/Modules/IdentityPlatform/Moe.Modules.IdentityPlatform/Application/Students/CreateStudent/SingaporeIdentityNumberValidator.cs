namespace Moe.Modules.IdentityPlatform.Application.Students.CreateStudent;

internal static class SingaporeIdentityNumberValidator
{
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

        for (int index = 1; index <= 7; index++)
        {
            char digit = normalized[index];
            if (!char.IsAsciiDigit(digit))
            {
                return false;
            }
        }

        char suffix = normalized[8];
        return char.IsAsciiLetterUpper(suffix);
    }
}
