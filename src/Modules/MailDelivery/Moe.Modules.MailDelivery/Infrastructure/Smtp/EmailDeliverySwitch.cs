using Microsoft.Extensions.Options;
using Moe.Modules.MailDelivery.IGateway;

namespace Moe.Modules.MailDelivery.Infrastructure.Smtp;

internal sealed class EmailDeliverySwitch(IOptionsMonitor<MailDeliveryOptions> options) : IEmailDeliverySwitch
{
    public bool IsEnabled => options.CurrentValue.Enabled;
}
