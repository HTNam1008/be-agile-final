using Microsoft.EntityFrameworkCore;
using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Security;
using Moe.Modules.IdentityPlatform.Domain.Schooling;
using Moe.Modules.MailDelivery.Application.Admin;
using Moe.Modules.MailDelivery.Domain;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.IdentityPlatform.Infrastructure.People;

internal sealed class MailNotificationAccessScope(
    MoeDbContext dbContext,
    IAdminAccessControl adminAccess,
    IClock clock) : IMailNotificationAccessScope
{
    public IQueryable<EmailNotification> Apply(IQueryable<EmailNotification> query)
    {
        if (adminAccess.IsHqAdmin)
        {
            return query;
        }

        if (!adminAccess.IsSchoolAdmin)
        {
            return query.Where(_ => false);
        }

        long[] scopedOrganizationIds = adminAccess.ScopedOrganizationIds
            .Where(x => x > 0)
            .Distinct()
            .ToArray();

        if (scopedOrganizationIds.Length == 0)
        {
            return query.Where(_ => false);
        }

        DateOnly today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        IQueryable<SchoolEnrollment> scopedEnrollments = dbContext.Set<SchoolEnrollment>()
            .AsNoTracking()
            .Where(enrollment =>
                scopedOrganizationIds.Contains(enrollment.OrganizationId)
                && enrollment.SchoolingStatusCode == "ACTIVE"
                && enrollment.StartDate <= today
                && (enrollment.EndDate == null || enrollment.EndDate >= today));

        return query.Where(notification =>
            scopedEnrollments.Any(enrollment => enrollment.PersonId == notification.PersonId));
    }
}
