using Moe.Modules.AiCopilot.Application.Security;
using Xunit;

namespace Moe.StudentFinance.IntegrationTests;

public sealed class AiCopilotRedactionTests
{
    private static readonly SensitiveDataRedactor Redactor = new();

    [Fact]
    public void Redact_email_address()
    {
        string result = Redactor.Redact("Contact me at john.doe@example.com for help.");
        Assert.DoesNotContain("john.doe@example.com", result);
        Assert.Contains("[EMAIL]", result);
    }

    [Fact]
    public void Redact_nric_fin()
    {
        string result = Redactor.Redact("My NRIC is S1234567A and my friend is T7654321B.");
        Assert.DoesNotContain("S1234567A", result);
        Assert.DoesNotContain("T7654321B", result);
        Assert.Contains("[IDENTITY]", result);
    }

    [Fact]
    public void Redact_phone_number()
    {
        string result = Redactor.Redact("Call me at 91234567 or +65 6123 4567.");
        Assert.DoesNotContain("91234567", result);
        Assert.Contains("[PHONE]", result);
    }

    [Fact]
    public void Redact_credential_pattern()
    {
        string result = Redactor.Redact("My password=secret123 and api_key=abc-def-ghi.");
        Assert.DoesNotContain("secret123", result);
        Assert.DoesNotContain("abc-def-ghi", result);
        Assert.Contains("[REDACTED]", result);
    }

    [Fact]
    public void Redact_multiple_sensitive_patterns_in_one_string()
    {
        string result = Redactor.Redact("User S1234567A email user@test.com phone 81234567 password=hidden");
        Assert.Contains("[IDENTITY]", result);
        Assert.Contains("[EMAIL]", result);
        Assert.Contains("[PHONE]", result);
        Assert.Contains("[REDACTED]", result);
    }

    [Fact]
    public void Redact_null_or_empty_returns_input()
    {
        Assert.Null(Redactor.Redact(null!));
        Assert.Equal("", Redactor.Redact(""));
        Assert.Equal("   ", Redactor.Redact("   "));
    }

    [Fact]
    public void Redact_short_string_preserves_length()
    {
        string result = Redactor.Redact("hello world");
        Assert.Equal("hello world", result);
    }

    [Fact]
    public void Redact_truncates_at_4000_characters()
    {
        string input = new string('a', 5000);
        string result = Redactor.Redact(input);
        Assert.Equal(4000, result.Length);
    }
}
