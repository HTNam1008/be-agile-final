using Moe.Modules.MailDelivery.IGateway;

namespace Moe.EducationAccountTopUp.UnitTests.TestDoubles;

internal sealed class FixedEmailDeliverySwitch(bool isEnabled = true) : IEmailDeliverySwitch
{
    public bool IsEnabled { get; } = isEnabled;
}
