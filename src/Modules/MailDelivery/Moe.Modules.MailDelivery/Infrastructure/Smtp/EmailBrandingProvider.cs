using Microsoft.Extensions.Options;
using Moe.Modules.MailDelivery.IGateway;

namespace Moe.Modules.MailDelivery.Infrastructure.Smtp;

internal sealed class EmailBrandingProvider(IOptionsMonitor<MailDeliveryOptions> options) : IEmailBrandingProvider
{
    public string AppName
        => string.IsNullOrWhiteSpace(options.CurrentValue.AppName)
            ? MailDeliveryOptions.DefaultAppName
            : options.CurrentValue.AppName.Trim();

    public string PaymentDashboardUrl => BuildPortalUrl("portal/payments");

    public string FasPortalUrl => BuildPortalUrl("portal/fas");

    public string AccountPortalUrl => BuildPortalUrl("portal/account");

    public string CourseDetailUrl(long courseId)
        => BuildPortalUrl($"portal/courses/{courseId}");

    private string BuildPortalUrl(string relativePath)
    {
        string baseUrl = string.IsNullOrWhiteSpace(options.CurrentValue.PortalBaseUrl)
            ? MailDeliveryOptions.DefaultPortalBaseUrl
            : options.CurrentValue.PortalBaseUrl.Trim();
        return $"{baseUrl.TrimEnd('/')}/{relativePath.TrimStart('/')}";
    }
}
