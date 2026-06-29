using Moe.SharedKernel.Results;

namespace Moe.Modules.MailDelivery.IGateway;

public interface IEmailDeliveryGateway
{
    Task<Result> SendAsync(EmailDeliveryMessage message, CancellationToken cancellationToken);
}
