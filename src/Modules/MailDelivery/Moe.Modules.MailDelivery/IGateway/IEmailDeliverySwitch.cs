namespace Moe.Modules.MailDelivery.IGateway;

public interface IEmailDeliverySwitch
{
    bool IsEnabled { get; }
}
