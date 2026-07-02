using Moe.Modules.MailDelivery.IGateway;

namespace Moe.FasPayment.UnitTests;

internal sealed class FixedEmailBrandingProvider(string appName = "Ministry of Education - Singapore") : IEmailBrandingProvider
{
    public string AppName { get; } = appName;

    public string PaymentDashboardUrl => "http://localhost:5173/portal/payments";

    public string FasPortalUrl => "http://localhost:5173/portal/fas";

    public string AccountPortalUrl => "http://localhost:5173/portal/account";

    public string CourseDetailUrl(long courseId) => $"http://localhost:5173/portal/courses/{courseId}";
}
