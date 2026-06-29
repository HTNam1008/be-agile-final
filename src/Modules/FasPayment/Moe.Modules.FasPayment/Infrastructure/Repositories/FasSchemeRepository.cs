using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Moe.Infrastructure.Shared.Api;
using Moe.Modules.FasPayment.Application.AdminFasSchemes;
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
        CreateFasSchemeRequest normalizedRequest = FasSchemeRequestDefaults.WithSystemGrantCode(request);
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
                    scheme.UpdateEditable(normalizedRequest.SchemeCode, normalizedRequest.GrantCode, normalizedRequest.Name, normalizedRequest.Description, normalizedRequest.StartDate, normalizedRequest.EndDate, actorId, utcNow);
                    await RemoveSchemeChildrenAsync(scheme.Id, cancellationToken);
                }
                else
                {
                    scheme = FasScheme.CreateDraft(normalizedRequest.SchemeCode, normalizedRequest.GrantCode, normalizedRequest.Name, normalizedRequest.Description,
                        normalizedRequest.StartDate, normalizedRequest.EndDate, actorId, utcNow);
                    dbContext.Add(scheme);
                    await dbContext.SaveChangesAsync(cancellationToken);
                }

                foreach (long courseId in normalizedRequest.CourseIds.Distinct()) dbContext.Add(FasSchemeCourse.Create(scheme.Id, courseId, utcNow));
                await dbContext.SaveChangesAsync(cancellationToken);

                foreach (CreateFasTierRequest tierRequest in normalizedRequest.Tiers.OrderBy(x => x.DisplayOrder))
                {
                    FasTier tier = FasTier.Create(scheme.Id, tierRequest.Label, normalizedRequest.SubsidyType, tierRequest.SubsidyValue, tierRequest.DisplayOrder, utcNow);
                    dbContext.Add(tier);
                    await dbContext.SaveChangesAsync(cancellationToken);
                    foreach (FasCriteriaTemplateItem template in normalizedRequest.CriteriaTemplate.OrderBy(x => x.DisplayOrder))
                    {
                        FasTierCriteriaValue value = tierRequest.CriteriaValues.Single(x => x.DisplayOrder == template.DisplayOrder);
                        FasTierCriteria criteria = FasTierCriteria.Create(tier.Id, template.CriteriaType, value.NumberFrom, value.NumberTo,
                            template.ConnectorToNext, template.DisplayOrder, utcNow);
                        dbContext.Add(criteria);
                        await dbContext.SaveChangesAsync(cancellationToken);
                        if (template.CriteriaType is "NATIONALITY" or "PARENT_NATIONALITY" or "ACCOUNT_TYPE")
                        {
                            foreach (string nationality in value.Nationalities!.Distinct(StringComparer.Ordinal))
                                dbContext.Add(FasTierCriteriaNationality.Create(criteria.Id, template.CriteriaType, nationality));
                            await dbContext.SaveChangesAsync(cancellationToken);
                        }
                    }
                }

                if (activate && scheme.StatusCode == "DRAFT") scheme.Activate(actorId, utcNow);
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

    public Task<CreateFasSchemeResponse?> PublishAsync(long schemeId, long actorId, DateTime utcNow, CancellationToken cancellationToken)
        => ChangeStatusAsync(schemeId, actorId, utcNow, "PUBLISH", cancellationToken);

    public Task<CreateFasSchemeResponse?> DisableAsync(long schemeId, long actorId, DateTime utcNow, CancellationToken cancellationToken)
        => ChangeStatusAsync(schemeId, actorId, utcNow, "DISABLE", cancellationToken);

    public Task<CreateFasSchemeResponse?> DeleteAsync(long schemeId, long actorId, DateTime utcNow, CancellationToken cancellationToken)
        => ChangeStatusAsync(schemeId, actorId, utcNow, "DELETE", cancellationToken);

    private async Task<CreateFasSchemeResponse?> ChangeStatusAsync(
        long schemeId,
        long actorId,
        DateTime utcNow,
        string transition,
        CancellationToken cancellationToken)
    {
        FasScheme? scheme = await dbContext.Set<FasScheme>().SingleOrDefaultAsync(x => x.Id == schemeId, cancellationToken);
        if (scheme is null) return null;

        if (transition == "PUBLISH") scheme.Activate(actorId, utcNow);
        else if (transition == "DISABLE") scheme.Disable(actorId, utcNow);
        else scheme.Delete(actorId, utcNow);

        if (transition is "DISABLE" or "DELETE")
        {
            await CancelUnavailableApplicationSelectionsAsync(
                scheme.Id,
                actorId,
                utcNow,
                transition == "DISABLE" ? "FAS scheme disabled by admin" : "FAS scheme deleted by admin",
                cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return new CreateFasSchemeResponse(scheme.Id, scheme.SchemeCode, scheme.GrantCode, scheme.StatusCode);
    }

    private async Task CancelUnavailableApplicationSelectionsAsync(
        long schemeId,
        long actorId,
        DateTime utcNow,
        string note,
        CancellationToken cancellationToken)
    {
        var selections = await dbContext.Set<FasApplicationScheme>()
            .Where(x => x.FasSchemeId == schemeId && (x.StatusCode == "DRAFT" || x.StatusCode == "PENDING"))
            .ToListAsync(cancellationToken);

        foreach (FasApplicationScheme selection in selections)
        {
            string oldStatus = selection.StatusCode;
            selection.CancelAsUnavailable();
            dbContext.Add(FasStatusHistory.Create(
                selection.FasApplicationId,
                selection.Id,
                oldStatus,
                "CANCELLED",
                note,
                actorId,
                "ADMIN",
                utcNow));
        }
    }

    public async Task<PageResponse<FasSchemeListItem>> ListAsync(string? status, string? search, int page, int pageSize, CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        IQueryable<FasScheme> query = dbContext.Set<FasScheme>().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(status))
        {
            string normalized = status.Trim().ToUpperInvariant();
            if (normalized == "NOT_STARTED")
            {
                DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);
                query = query.Where(x => x.StatusCode == FasSchemeStatusCodes.Active && x.StartDate > today);
            }
            else if (normalized == "CLOSED") query = query.Where(x => x.StatusCode == FasSchemeStatusCodes.Retired);
            else query = query.Where(x => x.StatusCode == normalized);
        }
        else query = query.Where(x => x.StatusCode != FasSchemeStatusCodes.Deleted);
        if (!string.IsNullOrWhiteSpace(search)) { string value = search.Trim(); query = query.Where(x => x.SchemeCode.Contains(value) || x.GrantCode.Contains(value) || x.Name.Contains(value)); }
        long totalCount = await query.LongCountAsync(cancellationToken);
        var schemes = await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        long[] ids = schemes.Select(x => x.Id).ToArray();
        var courses = await dbContext.Set<FasSchemeCourse>().AsNoTracking().Where(x => ids.Contains(x.FasSchemeId)).ToListAsync(cancellationToken);
        var applicationCounts = await dbContext.Set<FasApplicationScheme>().AsNoTracking()
            .Where(x => ids.Contains(x.FasSchemeId))
            .GroupBy(x => x.FasSchemeId)
            .Select(x => new { SchemeId = x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.SchemeId, x => x.Count, cancellationToken);
        return new PageResponse<FasSchemeListItem>(schemes.Select(x => new FasSchemeListItem(x.Id, x.SchemeCode, x.GrantCode, x.Name,
            x.Description, x.StartDate, x.EndDate, x.StatusCode, courses.Where(c => c.FasSchemeId == x.Id).Select(c => c.CourseId).Order().ToArray(),
            applicationCounts.GetValueOrDefault(x.Id))).ToArray(), page, pageSize, totalCount);
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
        if (tiers.Length == 0) throw Corrupt(schemeId, "scheme has no tiers");
        string subsidyType = tiers[0].SubsidyType;
        if (tiers.Any(x => x.SubsidyType != subsidyType)) throw Corrupt(schemeId, "tier subsidy types differ");
        FasTierCriteria[] firstCriteria = criteria.Where(x => x.FasTierId == tiers[0].Id).OrderBy(x => x.DisplayOrder).ToArray();
        foreach (FasTier tier in tiers.Skip(1))
        {
            FasTierCriteria[] candidate = criteria.Where(x => x.FasTierId == tier.Id).OrderBy(x => x.DisplayOrder).ToArray();
            if (candidate.Length != firstCriteria.Length || candidate.Where((x, i) => x.CriteriaType != firstCriteria[i].CriteriaType || x.ConnectorToNext != firstCriteria[i].ConnectorToNext || x.DisplayOrder != firstCriteria[i].DisplayOrder).Any())
                throw Corrupt(schemeId, "tier criteria templates differ");
        }
        var template = firstCriteria.Select(x => new FasCriteriaTemplateItem(x.CriteriaType, x.ConnectorToNext, x.DisplayOrder)).ToArray();
        var tierDetails = tiers.Select(t => new FasTierDetail(t.Id, t.Label, t.SubsidyValue, t.DisplayOrder,
            criteria.Where(c => c.FasTierId == t.Id).OrderBy(c => c.DisplayOrder).Select(c => new FasTierCriteriaValue(c.DisplayOrder, c.NumberFrom, c.NumberTo,
                c.CriteriaType is "NATIONALITY" or "PARENT_NATIONALITY" or "ACCOUNT_TYPE" ? nationalities.Where(n => n.FasTierCriteriaId == c.Id).Select(n => n.Nationality).Order().ToArray() : null)).ToArray())).ToArray();
        return new FasSchemeDetail(scheme.Id, scheme.SchemeCode, scheme.GrantCode, scheme.Name, scheme.Description,
            scheme.StartDate, scheme.EndDate, scheme.StatusCode, courseIds, subsidyType, template, tierDetails);
    }

    private InvalidOperationException Corrupt(long schemeId, string reason)
    {
        logger.LogError("FAS scheme {SchemeId} contains inconsistent duplicated data: {Reason}", schemeId, reason);
        return new InvalidOperationException($"FAS scheme {schemeId} contains inconsistent data.");
    }
}
