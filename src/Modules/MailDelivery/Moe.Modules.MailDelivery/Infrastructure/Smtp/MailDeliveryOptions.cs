using System.ComponentModel.DataAnnotations;

namespace Moe.Modules.MailDelivery.Infrastructure.Smtp;

public sealed class MailDeliveryOptions
{
    public const string SectionName = "MailDelivery";

    [Required]
    public string AppName { get; init; } = "MOE SEEDS";

    [Required]
    public string Host { get; init; } = "smtp.gmail.com";

    [Range(1, 65535)]
    public int Port { get; init; } = 587;

    public bool EnableSsl { get; init; } = true;

    [Required]
    public string UserName { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;

    [Required]
    [EmailAddress]
    public string FromEmail { get; init; } = string.Empty;

    [Required]
    public string FromDisplayName { get; init; } = "MOE SEEDS";
}
