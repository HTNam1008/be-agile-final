namespace Moe.Modules.MailDelivery.IGateway;

public interface IEmailBrandingProvider
{
    string AppName { get; }

    string PaymentDashboardUrl { get; }

    string FasPortalUrl { get; }

    string AccountPortalUrl { get; }

    string CourseDetailUrl(long courseId);
}
