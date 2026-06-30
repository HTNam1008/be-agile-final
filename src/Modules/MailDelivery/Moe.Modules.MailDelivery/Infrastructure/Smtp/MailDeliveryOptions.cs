using System.Net.Mail;

namespace Moe.Modules.MailDelivery.Infrastructure.Smtp;

public sealed class MailDeliveryOptions
{
    public const string SectionName = "MailDelivery";

    public bool Enabled { get; init; } = true;

    public string AppName { get; init; } = "MOE SEEDS";

    public string Host { get; init; } = "smtp.gmail.com";

    public int Port { get; init; } = 587;

    public bool EnableSsl { get; init; } = true;

    public string UserName { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;

    public string FromEmail { get; init; } = string.Empty;

    public string FromDisplayName { get; init; } = "MOE SEEDS";

    public string? DevelopmentFallbackRecipient { get; init; }

    public static bool IsValid(MailDeliveryOptions options)
    {
        if (!options.Enabled)
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(options.AppName)
            && !string.IsNullOrWhiteSpace(options.Host)
            && options.Port is >= 1 and <= 65535
            && !string.IsNullOrWhiteSpace(options.UserName)
            && IsValidEmail(options.FromEmail)
            && !string.IsNullOrWhiteSpace(options.FromDisplayName)
            && (string.IsNullOrWhiteSpace(options.DevelopmentFallbackRecipient)
                || IsValidEmail(options.DevelopmentFallbackRecipient));
    }

    private static bool IsValidEmail(string? emailAddress)
        => !string.IsNullOrWhiteSpace(emailAddress)
            && MailAddress.TryCreate(emailAddress.Trim(), out _);
}
