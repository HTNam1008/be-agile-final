using System.Text.RegularExpressions;

namespace Moe.Modules.AiCopilot.Application.Security;

public sealed partial class SensitiveDataRedactor
{
    public string Redact(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return value;
        string result = EmailRegex().Replace(value, "[EMAIL]");
        result = NricRegex().Replace(result, "[IDENTITY]");
        result = PhoneRegex().Replace(result, "[PHONE]");
        result = CredentialRegex().Replace(result, "$1=[REDACTED]");
        return result.Length <= 4000 ? result : result[..4000];
    }

    [GeneratedRegex(@"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}\b", RegexOptions.IgnoreCase)]
    private static partial Regex EmailRegex();
    [GeneratedRegex(@"\b[STFGM]\d{7}[A-Z]\b", RegexOptions.IgnoreCase)]
    private static partial Regex NricRegex();
    [GeneratedRegex(@"(?<!\d)(?:\+?65[ -]?)?[689]\d{3}[ -]?\d{4}(?!\d)")]
    private static partial Regex PhoneRegex();
    [GeneratedRegex(@"\b(password|api[_ -]?key|secret|token)\s*[:=]\s*\S+", RegexOptions.IgnoreCase)]
    private static partial Regex CredentialRegex();
}
