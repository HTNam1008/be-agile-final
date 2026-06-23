using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Moe.Modules.FasPayment.Contracts.AdminFasSchemes;
using Moe.Modules.FasPayment.Domain.Fas;
using Moe.Modules.FasPayment.IGateway.Repositories;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.FasPayment.Infrastructure.Repositories;

internal sealed class FasSchemeRepository(MoeDbContext dbContext, ILogger<FasSchemeRepository> logger) : IFasSchemeRepository
{
    public Task<bool> SchemeCodeExistsAsync(string schemeCode, CancellationToken cancellationToken)
        => dbContext.Set<FasScheme>().AnyAsync(x => x.SchemeCode == schemeCode.Trim(), cancellationToken);

    public Task<bool> GrantCodeExistsAsync(string grantCode, CancellationToken cancellationToken)
        => dbContext.Set<FasScheme>().AnyAsync(x => x.GrantCode == grantCode.Trim(), cancellationToken);

    public Task<bool> SchemeCodeExistsExcludingAsync(string schemeCode, long excludedSchemeId, CancellationToken cancellationToken)
        => dbContext.Set<FasScheme>().AnyAsync(x => x.SchemeCode == schemeCode.Trim() && x.Id != excludedSchemeId, cancellationToken);

    public Task<bool> GrantCodeExistsExcludingAsync(string grantCode, long excludedSchemeId, CancellationToken cancellationToken)
        => dbContext.Set<FasScheme>().AnyAsync(x => x.GrantCode == grantCode.Trim() && x.Id != excludedSchemeId, cancellationToken);

    public Task<CreateFasSchemeResponse> CreateAsync(CreateFasSchemeRequest request, long actorId, DateTime utcNow, CancellationToken cancellationToken)
        => SaveAsync(null, request, actorId, utcNow, activate: true, cancellationToken);

    public Task<CreateFasSchemeResponse> SaveDraftAsync(long? schemeId, CreateFasSchemeRequest request, long actorId, DateTime utcNow, CancellationToken cancellationToken)
        => SaveAsync(schemeId, request, actorId, utcNow, activate: false, cancellationToken);

    public Task<CreateFasSchemeResponse> ActivateDraftAsync(long schemeId, CreateFasSchemeRequest request, long actorId, DateTime utcNow, CancellationToken cancellationToken)
        => SaveAsync(schemeId, request, actorId, utcNow, activate: true, cancellationToken);

    private async Task<CreateFasSchemeResponse> SaveAsync(long? schemeId, CreateFasSchemeRequest request, long actorId, DateTime utcNow, bool activate, CancellationToken cancellationToken)
    {
        IExecutionStrategy strategy = dbContext.Database.CreateExecutionStrategy();
        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                await using IDbContextTransaction? transaction = dbContext.Database.ProviderName == "Microsoft.EntityFrameworkCore.SqlServer"
                    ? await dbContext.Database.BeginTransactionAsync(cancellationToken)
                    : null;

                FasScheme scheme;
                if (schemeId.HasValue)
                {
                    scheme = await dbContext.Set<FasScheme>().SingleAsync(x => x.Id == schemeId.Value, cancellationToken);
                    scheme.UpdateDraft(request.SchemeCode, request.GrantCode, request.Name, request.Description, request.StartDate, request.EndDate, actorId, utcNow);
                    await RemoveSchemeChildrenAsync(scheme.Id, cancellationToken);
                }
                else
                {
                    scheme = FasScheme.CreateDraft(request.SchemeCode, request.GrantCode, request.Name, request.Description,
                        request.StartDate, request.EndDate, actorId, utcNow);
                    dbContext.Add(scheme);
                    await dbContext.SaveChangesAsync(cancellationToken);
                }

                foreach (long courseId in request.CourseIds.Distinct()) dbContext.Add(FasSchemeCourse.Create(scheme.Id, courseId, utcNow));
                await dbContext.SaveChangesAsync(cancellationToken);

                foreach (CreateFasTierRequest tierRequest in request.Tiers.OrderBy(x => x.DisplayOrder))
                {
                    FasTier tier = FasTier.Create(scheme.Id, tierRequest.Label, tierRequest.SubsidyType, tierRequest.SubsidyValue, tierRequest.DisplayOrder, utcNow);
                    dbContext.Add(tier);
                    await dbContext.SaveChangesAsync(cancellationToken);
                    foreach (FasTierCriteriaRequest criteriaReq in tierRequest.Criteria.OrderBy(x => x.DisplayOrder))
                    {
                        FasTierCriteria criteria = FasTierCriteria.Create(tier.Id, criteriaReq.CriteriaType, criteriaReq.NumberFrom, criteriaReq.NumberTo,
                            criteriaReq.ConnectorToNext, criteriaReq.DisplayOrder, utcNow);
                        dbContext.Add(criteria);
                        await dbContext.SaveChangesAsync(cancellationToken);
                        if (criteriaReq.CriteriaType == "NATIONALITY" && criteriaReq.Nationalities is not null)
                        {
                            foreach (string nationality in criteriaReq.Nationalities.Distinct(StringComparer.Ordinal))
                                dbContext.Add(FasTierCriteriaNationality.Create(criteria.Id, nationality));
                            await dbContext.SaveChangesAsync(cancellationToken);
                        }
                    }
                }

                if (activate) scheme.Activate(actorId, utcNow);
                await dbContext.SaveChangesAsync(cancellationToken);
                if (transaction is not null) await transaction.CommitAsync(cancellationToken);
                return new CreateFasSchemeResponse(scheme.Id, scheme.SchemeCode, scheme.GrantCode, scheme.StatusCode);
            });
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 } sqlException)
        {
            FasSchemeUniqueField field = sqlException.Message.Contains("GrantCode", StringComparison.OrdinalIgnoreCase)
                ? FasSchemeUniqueField.GrantCode
                : sqlException.Message.Contains("SchemeCode", StringComparison.OrdinalIgnoreCase)
                    ? FasSchemeUniqueField.SchemeCode
                    : FasSchemeUniqueField.Unknown;
            throw new FasSchemeWriteConflictException(field, exception);
        }
    }

    private async Task RemoveSchemeChildrenAsync(long schemeId, CancellationToken cancellationToken)
    {
        FasTier[] tiers = await dbContext.Set<FasTier>().Where(x => x.FasSchemeId == schemeId).ToArrayAsync(cancellationToken);
        long[] tierIds = tiers.Select(x => x.Id).ToArray();
        FasTierCriteria[] criteria = await dbContext.Set<FasTierCriteria>().Where(x => tierIds.Contains(x.FasTierId)).ToArrayAsync(cancellationToken);
        long[] criteriaIds = criteria.Select(x => x.Id).ToArray();
        dbContext.RemoveRange(await dbContext.Set<FasTierCriteriaNationality>().Where(x => criteriaIds.Contains(x.FasTierCriteriaId)).ToArrayAsync(cancellationToken));
        dbContext.RemoveRange(criteria);
        dbContext.RemoveRange(tiers);
        dbContext.RemoveRange(await dbContext.Set<FasSchemeCourse>().Where(x => x.FasSchemeId == schemeId).ToArrayAsync(cancellationToken));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> DeleteDraftAsync(long schemeId, CancellationToken cancellationToken)
    {
        FasScheme? scheme = await dbContext.Set<FasScheme>().SingleOrDefaultAsync(x => x.Id == schemeId && x.StatusCode == "DRAFT", cancellationToken);
        if (scheme is null) return false;
        IExecutionStrategy strategy = dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using IDbContextTransaction? transaction = dbContext.Database.ProviderName == "Microsoft.EntityFrameworkCore.SqlServer"
                ? await dbContext.Database.BeginTransactionAsync(cancellationToken)
                : null;
            await RemoveSchemeChildrenAsync(schemeId, cancellationToken);
            dbContext.Remove(scheme);
            await dbContext.SaveChangesAsync(cancellationToken);
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        });
        return true;
    }

    public async Task<FasSchemeListResponse> ListAsync(string? status, string? search, CancellationToken cancellationToken)
    {
        IQueryable<FasScheme> query = dbContext.Set<FasScheme>().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(status)) { string normalized = status.Trim().ToUpperInvariant(); query = query.Where(x => x.StatusCode == normalized); }
        if (!string.IsNullOrWhiteSpace(search)) { string value = search.Trim(); query = query.Where(x => x.SchemeCode.Contains(value) || x.GrantCode.Contains(value) || x.Name.Contains(value)); }
        var schemes = await query.OrderByDescending(x => x.CreatedAtUtc).ToListAsync(cancellationToken);
        long[] ids = schemes.Select(x => x.Id).ToArray();
        var courses = await dbContext.Set<FasSchemeCourse>().AsNoTracking().Where(x => ids.Contains(x.FasSchemeId)).ToListAsync(cancellationToken);
        return new FasSchemeListResponse(schemes.Select(x => new FasSchemeListItem(x.Id, x.SchemeCode, x.GrantCode, x.Name,
            x.Description, x.StartDate, x.EndDate, x.StatusCode, courses.Where(c => c.FasSchemeId == x.Id).Select(c => c.CourseId).Order().ToArray())).ToArray());
    }

    public async Task<FasSchemeDetail?> GetAsync(long schemeId, CancellationToken cancellationToken)
    {
        FasScheme? scheme = await dbContext.Set<FasScheme>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == schemeId, cancellationToken);
        if (scheme is null) return null;
        long[] courseIds = await dbContext.Set<FasSchemeCourse>().AsNoTracking().Where(x => x.FasSchemeId == schemeId).OrderBy(x => x.CourseId).Select(x => x.CourseId).ToArrayAsync(cancellationToken);
        FasTier[] tiers = await dbContext.Set<FasTier>().AsNoTracking().Where(x => x.FasSchemeId == schemeId).OrderBy(x => x.DisplayOrder).ToArrayAsync(cancellationToken);
        long[] tierIds = tiers.Select(x => x.Id).ToArray();
        FasTierCriteria[] criteria = await dbContext.Set<FasTierCriteria>().AsNoTracking().Where(x => tierIds.Contains(x.FasTierId)).OrderBy(x => x.DisplayOrder).ToArrayAsync(cancellationToken);
        long[] criteriaIds = criteria.Select(x => x.Id).ToArray();
        FasTierCriteriaNationality[] nationalities = await dbContext.Set<FasTierCriteriaNationality>().AsNoTracking().Where(x => criteriaIds.Contains(x.FasTierCriteriaId)).ToArrayAsync(cancellationToken);
        var tierDetails = tiers.Select(t => new FasTierDetail(t.Id, t.Label, t.SubsidyType, t.SubsidyValue, t.DisplayOrder,
            criteria.Where(c => c.FasTierId == t.Id).OrderBy(c => c.DisplayOrder).Select(c => new FasTierCriteriaDetail(c.Id, c.CriteriaType, c.NumberFrom, c.NumberTo,
                c.CriteriaType == "NATIONALITY" ? nationalities.Where(n => n.FasTierCriteriaId == c.Id).Select(n => n.Nationality).Order().ToArray() : null, c.ConnectorToNext, c.DisplayOrder)).ToArray())).ToArray();
        return new FasSchemeDetail(scheme.Id, scheme.SchemeCode, scheme.GrantCode, scheme.Name, scheme.Description,
            scheme.StartDate, scheme.EndDate, scheme.StatusCode, courseIds, tierDetails);
    }

    private InvalidOperationException Corrupt(long schemeId, string reason)
    {
        logger.LogError("FAS scheme {SchemeId} contains inconsistent duplicated data: {Reason}", schemeId, reason);
        return new InvalidOperationException($"FAS scheme {schemeId} contains inconsistent data.");
    }
}
