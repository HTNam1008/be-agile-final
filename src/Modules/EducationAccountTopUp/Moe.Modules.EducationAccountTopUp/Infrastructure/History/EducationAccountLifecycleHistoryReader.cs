using Microsoft.EntityFrameworkCore;
using Moe.Modules.EducationAccountTopUp.Application.History;
using Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;
using Moe.Modules.EducationAccountTopUp.Domain.Lifecycle;
using Moe.Modules.EducationAccountTopUp.IGateway.History;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.EducationAccountTopUp.Infrastructure.History;

internal sealed class EducationAccountLifecycleHistoryReader(MoeDbContext dbContext)
    : IEducationAccountLifecycleHistoryReader
{
    public async Task<HistoryPage<EducationAccountLifecycleRunProjection>> ListRunsAsync(
        DateOnly? fromDate,
        DateOnly? toDate,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        IQueryable<EducationAccountLifecycleRun> query =
            dbContext.Set<EducationAccountLifecycleRun>().AsNoTracking();

        if (fromDate.HasValue)
        {
            query = query.Where(x => x.RunDateUtc >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(x => x.RunDateUtc <= toDate.Value);
        }

        long totalCount = await query.LongCountAsync(cancellationToken);
        EducationAccountLifecycleRunProjection[] items = await query
            .OrderByDescending(x => x.StartedAtUtc)
            .ThenByDescending(x => x.Id)
            .Skip(CalculateSkip(page, pageSize))
            .Take(pageSize)
            .Select(x => new EducationAccountLifecycleRunProjection(
                x.Id,
                x.RunDateUtc,
                x.StartedAtUtc,
                x.CompletedAtUtc,
                x.TriggerTypeCode,
                x.StatusCode,
                x.OpenedCount,
                x.ClosedCount,
                x.ErrorMessage))
            .ToArrayAsync(cancellationToken);

        return new HistoryPage<EducationAccountLifecycleRunProjection>(items, totalCount);
    }

    public async Task<EducationAccountLifecycleRunDetailProjection?> GetRunDetailAsync(
        long runId,
        CancellationToken cancellationToken)
    {
        EducationAccountLifecycleRunProjection? run = await dbContext
            .Set<EducationAccountLifecycleRun>()
            .AsNoTracking()
            .Where(x => x.Id == runId)
            .Select(x => new EducationAccountLifecycleRunProjection(
                x.Id,
                x.RunDateUtc,
                x.StartedAtUtc,
                x.CompletedAtUtc,
                x.TriggerTypeCode,
                x.StatusCode,
                x.OpenedCount,
                x.ClosedCount,
                x.ErrorMessage))
            .SingleOrDefaultAsync(cancellationToken);

        if (run is null)
        {
            return null;
        }

        EducationAccountLifecycleRunItemProjection[] items = await (
                from item in dbContext.Set<EducationAccountLifecycleRunItem>().AsNoTracking()
                join account in dbContext.Set<EducationAccount>().AsNoTracking()
                    on item.EducationAccountId equals account.Id
                where item.EducationAccountLifecycleRunId == runId
                orderby item.OccurredAtUtc, item.Id
                select new EducationAccountLifecycleRunItemProjection(
                    item.Id,
                    item.PersonId,
                    item.EducationAccountId,
                    account.AccountNumber,
                    item.ActionCode,
                    item.OccurredAtUtc))
            .ToArrayAsync(cancellationToken);

        return new EducationAccountLifecycleRunDetailProjection(
            run.RunId,
            run.RunDateUtc,
            run.StartedAtUtc,
            run.CompletedAtUtc,
            run.TriggerTypeCode,
            run.StatusCode,
            run.OpenedCount,
            run.ClosedCount,
            run.ErrorMessage,
            items);
    }

    private static int CalculateSkip(int page, int pageSize)
        => checked((page - 1) * pageSize);
}
