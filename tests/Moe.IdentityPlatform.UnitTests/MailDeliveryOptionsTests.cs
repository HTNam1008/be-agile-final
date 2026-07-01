using FluentAssertions;
using Moe.Modules.MailDelivery.Infrastructure.Smtp;
using Moe.Modules.MailDelivery.Templates;
using System.Text;
using Xunit;

namespace Moe.IdentityPlatform.UnitTests;

public sealed class MailDeliveryOptionsTests
{
    [Fact]
    public void Defaults_UseMinistryBrandAndSenderDisplayName()
    {
        MailDeliveryOptions options = new();

        options.AppName.Should().Be("Ministry of Education - Singapore");
        options.FromDisplayName.Should().Be("Ministry of Education - Singapore");
        options.PortalBaseUrl.Should().Be("http://localhost:5173");
    }

    [Fact]
    public void AppendHeader_UsesConfiguredAppName()
    {
        StringBuilder builder = new();

        EmailTemplateBranding.AppendHeader(builder, "Payment Received", "Configured Brand");

        builder.ToString().Should().Contain("Configured Brand");
        builder.ToString().Should().NotContain("MOE SEEDS");
    }

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
            AppName = "Ministry of Education - Singapore",
            Host = string.Empty,
            Port = 587,
            UserName = "sender@example.com",
            FromEmail = "sender@example.com",
            FromDisplayName = "Ministry of Education - Singapore"
        };

        MailDeliveryOptions.IsValid(options).Should().BeFalse();
    }

    [Fact]
    public void IsValid_WhenFallbackIsConfigured_RequiresCompleteFallbackConfiguration()
    {
        MailDeliveryOptions validOptions = new()
        {
            Enabled = true,
            AppName = "Ministry of Education - Singapore",
            Host = "smtp.gmail.com",
            Port = 587,
            UserName = "primary@example.com",
            FromEmail = "primary@example.com",
            FromDisplayName = "Ministry of Education - Singapore",
            FallbackUserName = "fallback@example.com",
            FallbackPassword = "fallback-password",
            FallbackFromEmail = "fallback@example.com"
        };
        MailDeliveryOptions invalidOptions = new()
        {
            Enabled = true,
            AppName = "Ministry of Education - Singapore",
            Host = "smtp.gmail.com",
            Port = 587,
            UserName = "primary@example.com",
            FromEmail = "primary@example.com",
            FromDisplayName = "Ministry of Education - Singapore",
            FallbackUserName = "fallback@example.com",
            FallbackPassword = "fallback-password",
            FallbackFromEmail = "not-an-email"
        };

        MailDeliveryOptions.IsValid(validOptions).Should().BeTrue();
        MailDeliveryOptions.IsValid(invalidOptions).Should().BeFalse();
    }
}
