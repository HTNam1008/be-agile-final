using FluentAssertions;
using Moe.Modules.MailDelivery.Infrastructure.Smtp;
using Xunit;

namespace Moe.IdentityPlatform.UnitTests;

public sealed class MailDeliveryOptionsTests
{
    [Fact]
    public void IsValid_WhenDisabled_DoesNotRequireSmtpConfiguration()
    {
        MailDeliveryOptions options = new()
        {
            Enabled = false,
            AppName = string.Empty,
            Host = string.Empty,
            Port = 0,
            UserName = string.Empty,
            FromEmail = string.Empty,
            FromDisplayName = string.Empty
        };

        MailDeliveryOptions.IsValid(options).Should().BeTrue();
    }

    [Fact]
    public void IsValid_WhenEnabled_RequiresSmtpConfiguration()
    {
        MailDeliveryOptions options = new()
        {
            Enabled = true,
            AppName = "MOE SEEDS",
            Host = string.Empty,
            Port = 587,
            UserName = "sender@example.com",
            FromEmail = "sender@example.com",
            FromDisplayName = "MOE SEEDS"
        };

        MailDeliveryOptions.IsValid(options).Should().BeFalse();
    }

    [Fact]
    public void IsValid_WhenFallbackIsConfigured_RequiresCompleteFallbackConfiguration()
    {
        MailDeliveryOptions validOptions = new()
        {
            Enabled = true,
            AppName = "MOE SEEDS",
            Host = "smtp.gmail.com",
            Port = 587,
            UserName = "primary@example.com",
            FromEmail = "primary@example.com",
            FromDisplayName = "MOE SEEDS",
            FallbackUserName = "fallback@example.com",
            FallbackPassword = "fallback-password",
            FallbackFromEmail = "fallback@example.com"
        };
        MailDeliveryOptions invalidOptions = new()
        {
            Enabled = true,
            AppName = "MOE SEEDS",
            Host = "smtp.gmail.com",
            Port = 587,
            UserName = "primary@example.com",
            FromEmail = "primary@example.com",
            FromDisplayName = "MOE SEEDS",
            FallbackUserName = "fallback@example.com",
            FallbackPassword = "fallback-password",
            FallbackFromEmail = "not-an-email"
        };

        MailDeliveryOptions.IsValid(validOptions).Should().BeTrue();
        MailDeliveryOptions.IsValid(invalidOptions).Should().BeFalse();
    }
}
