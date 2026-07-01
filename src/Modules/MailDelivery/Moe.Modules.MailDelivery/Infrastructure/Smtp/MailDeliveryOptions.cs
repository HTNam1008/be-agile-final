using System.Net.Mail;

namespace Moe.Modules.MailDelivery.Infrastructure.Smtp;

public sealed class MailDeliveryOptions
{
    public const string SectionName = "MailDelivery";
    public const string DefaultAppName = "Ministry of Education - Singapore";
    public const string DefaultPortalBaseUrl = "https://femoegovsg.azurewebsites.net";

    public bool Enabled { get; init; } = true;

    public string AppName { get; init; } = DefaultAppName;

    public string PortalBaseUrl { get; init; } = DefaultPortalBaseUrl;

    public string Host { get; init; } = "smtp.gmail.com";

    public int Port { get; init; } = 587;

    public bool EnableSsl { get; init; } = true;

    public string UserName { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;

    public string FromEmail { get; init; } = string.Empty;

    public string FromDisplayName { get; init; } = DefaultAppName;

    public string? FallbackUserName { get; init; }

    public string? FallbackPassword { get; init; }

    public string? FallbackFromEmail { get; init; }

    public string? FallbackFromDisplayName { get; init; }

    public string? DevelopmentFallbackRecipient { get; init; }

    public bool HasFallbackSender
        => !string.IsNullOrWhiteSpace(FallbackUserName)
            && !string.IsNullOrWhiteSpace(FallbackPassword)
            && IsValidEmail(FallbackFromEmail);

    public static bool IsValid(MailDeliveryOptions options)
    {
        if (!options.Enabled)
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(options.AppName)
            && Uri.TryCreate(options.PortalBaseUrl, UriKind.Absolute, out _)
            && !string.IsNullOrWhiteSpace(options.Host)
            && options.Port is >= 1 and <= 65535
            && !string.IsNullOrWhiteSpace(options.UserName)
            && IsValidEmail(options.FromEmail)
            && !string.IsNullOrWhiteSpace(options.FromDisplayName)
            && IsValidFallback(options)
            && (string.IsNullOrWhiteSpace(options.DevelopmentFallbackRecipient)
                || IsValidEmail(options.DevelopmentFallbackRecipient));
    }

    private static bool IsValidFallback(MailDeliveryOptions options)
    {
        bool hasAnyFallbackValue = !string.IsNullOrWhiteSpace(options.FallbackUserName)
            || !string.IsNullOrWhiteSpace(options.FallbackPassword)
            || !string.IsNullOrWhiteSpace(options.FallbackFromEmail)
            || !string.IsNullOrWhiteSpace(options.FallbackFromDisplayName);

        return !hasAnyFallbackValue || options.HasFallbackSender;
    }

    private static bool IsValidEmail(string? emailAddress)
        => !string.IsNullOrWhiteSpace(emailAddress)
            && MailAddress.TryCreate(emailAddress.Trim(), out _);
}
