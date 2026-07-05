using Moe.Modules.MailDelivery.IGateway;

namespace Moe.FasPayment.UnitTests;

internal sealed class FixedEmailBrandingProvider(string appName = "Ministry of Education - Singapore") : IEmailBrandingProvider
{
    public string AppName { get; } = appName;

    public string PaymentDashboardUrl => "https://portal.example.test/portal/payments";

    public string FasPortalUrl => "https://portal.example.test/portal/fas";

    public string AccountPortalUrl => "https://portal.example.test/portal/account";

    public string CourseDetailUrl(long courseId) => $"https://portal.example.test/portal/courses/{courseId}";
}
