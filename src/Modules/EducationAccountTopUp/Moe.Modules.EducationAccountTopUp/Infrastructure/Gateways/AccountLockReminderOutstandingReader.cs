using Microsoft.EntityFrameworkCore;
using Moe.Modules.EducationAccountTopUp.Application.Lifecycle;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.EducationAccountTopUp.Infrastructure.Gateways;

internal sealed class AccountLockReminderOutstandingReader(MoeDbContext dbContext)
    : IAccountLockReminderOutstandingReader
{
    public async Task<decimal?> FindOutstandingAmountAsync(
        long personId,
        CancellationToken cancellationToken)
    {
        if (string.Equals(
                dbContext.Database.ProviderName,
                "Microsoft.EntityFrameworkCore.InMemory",
                StringComparison.Ordinal))
        {
            return null;
        }

        decimal outstandingAmount = await dbContext.Database.SqlQuery<decimal>($"""
            SELECT CAST(COALESCE(SUM(bill.[OutstandingAmount]), 0) AS decimal(19, 2)) AS [Value]
            FROM [billing].[Bill] bill
            INNER JOIN [course].[CourseEnrollment] enrollment
                ON enrollment.[CourseEnrollmentId] = bill.[CourseEnrollmentId]
            WHERE enrollment.[PersonId] = {personId}
              AND bill.[OutstandingAmount] > 0
              AND bill.[BillStatusCode] NOT IN ('PAID', 'CANCELLED')
            """)
            .SingleAsync(cancellationToken);

        return outstandingAmount > 0m ? outstandingAmount : null;
    }
}

