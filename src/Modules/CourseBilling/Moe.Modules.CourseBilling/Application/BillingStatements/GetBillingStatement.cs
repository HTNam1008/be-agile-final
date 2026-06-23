using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Security;
using Moe.Modules.CourseBilling.Contracts.BillingStatements;
using Moe.Modules.CourseBilling.Domain.Courses;
using Moe.Modules.CourseBilling.IGateway.Repositories;
using Moe.SharedKernel.Results;
using Moe.Modules.CourseBilling.Application;

namespace Moe.Modules.CourseBilling.Application.BillingStatements;

public sealed record GetBillingStatementQuery(int Year, int Month)
    : IQuery<BillingStatementResponse>;

internal sealed class GetBillingStatementHandler(
    IBillingStatementRepository statements,
    ICurrentUser currentUser,
    IClock clock) : IQueryHandler<GetBillingStatementQuery, BillingStatementResponse>
{
    public async Task<Result<BillingStatementResponse>> Handle(
        GetBillingStatementQuery query,
        CancellationToken cancellationToken)
    {
        if (!currentUser.TryGetStudent(out long personId))
            return Result<BillingStatementResponse>.Failure(CourseBillingErrors.StudentIdentityRequired);
        if (query.Year < 2000 || query.Month is < 1 or > 12)
            return Result<BillingStatementResponse>.Failure(CourseBillingErrors.InvalidStatementPeriod);
        return Result<BillingStatementResponse>.Success(await statements.GetOrCreateAsync(
            personId,
            query.Year,
            query.Month,
            clock.UtcNow.UtcDateTime,
            cancellationToken));
    }
}
