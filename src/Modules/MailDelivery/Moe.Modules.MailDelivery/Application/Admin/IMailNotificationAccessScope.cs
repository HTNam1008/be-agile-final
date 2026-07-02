using Microsoft.EntityFrameworkCore;
using Moe.Modules.MailDelivery.Domain;

namespace Moe.Modules.MailDelivery.Application.Admin;

public interface IMailNotificationAccessScope
{
    IQueryable<EmailNotification> Apply(IQueryable<EmailNotification> query);
}

internal sealed class AllowAllMailNotificationAccessScope : IMailNotificationAccessScope
{
    public IQueryable<EmailNotification> Apply(IQueryable<EmailNotification> query) => query;
}
