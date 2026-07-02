using Microsoft.EntityFrameworkCore;
using Moe.Modules.EducationAccountTopUp.Contracts.TopUps.Enums;
using Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;
using Moe.Modules.EducationAccountTopUp.IGateway.TopUps;
using Moe.Modules.IdentityPlatform.Domain.People;
using Moe.Modules.IdentityPlatform.Domain.Schooling;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.EducationAccountTopUp.Infrastructure.TopUps;

/// <summary>
/// EF Core implementation of IDynamicRuleFilter.
/// This is the SINGLE authorised location for dynamic rule query composition.
/// The Application layer never sees MoeDbContext.
/// </summary>
internal sealed class DynamicRuleFilter(MoeDbContext dbContext) : IDynamicRuleFilter
{
    public async Task<IReadOnlyList<long>> FilterAccountIdsAsync(
        IReadOnlyList<CampaignRuleGroupProjection> groups,
        int skip,
        int take,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        HashSet<long> matchedIds = await GetMatchingAccountIdsAsync(groups, nowUtc, cancellationToken);

        return matchedIds
            .OrderBy(x => x)
            .Skip(skip)
            .Take(take)
            .ToList();
    }

    public async Task<int> CountMatchingAccountsAsync(
        IReadOnlyList<CampaignRuleGroupProjection> groups,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        HashSet<long> matchedIds = await GetMatchingAccountIdsAsync(groups, nowUtc, cancellationToken);
        return matchedIds.Count;
    }

    private async Task<HashSet<long>> GetMatchingAccountIdsAsync(
        IReadOnlyList<CampaignRuleGroupProjection> groups,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var matchedIds = new HashSet<long>();

        foreach (CampaignRuleGroupProjection group in groups.Where(g => g.Criteria.Count > 0))
        {
            IQueryable<EducationAccount> query = BuildBaseQuery(group.Criteria, nowUtc);
            List<long> groupIds = await query.Select(x => x.Id).ToListAsync(cancellationToken);
            matchedIds.UnionWith(groupIds);
        }

        return matchedIds;
    }

    private IQueryable<EducationAccount> BuildBaseQuery(
        IReadOnlyList<CampaignRuleProjection> rules,
        DateTime nowUtc)
    {
        IQueryable<EducationAccount> query = dbContext.Set<EducationAccount>()
            .AsNoTracking()
            .Where(x => x.StatusCode == AccountStatuses.Active);

        bool requiresPerson = rules.Any(r =>
            r.CriterionCode.Equals(TopUpCriterionCode.Age.ToString(), StringComparison.OrdinalIgnoreCase));

        bool requiresEnrollment = rules.Any(r =>
            r.CriterionCode.Equals(TopUpCriterionCode.SchoolingStatus.ToString(), StringComparison.OrdinalIgnoreCase) ||
            r.CriterionCode.Equals(TopUpCriterionCode.Level.ToString(), StringComparison.OrdinalIgnoreCase) ||
            r.CriterionCode.Equals(TopUpCriterionCode.Class.ToString(), StringComparison.OrdinalIgnoreCase));

        if (requiresPerson)
            query = ApplyAgeRules(query, rules, nowUtc);

        if (requiresEnrollment)
            query = ApplyEnrollmentRules(query, rules, nowUtc);

        query = ApplyEducationAccountRules(query, rules);
        query = ApplyBalanceRules(query, rules);

        return query;
    }

    private IQueryable<EducationAccount> ApplyAgeRules(
        IQueryable<EducationAccount> query,
        IReadOnlyList<CampaignRuleProjection> rules,
        DateTime nowUtc)
    {
        foreach (CampaignRuleProjection rule in rules.Where(r =>
            r.CriterionCode.Equals(TopUpCriterionCode.Age.ToString(), StringComparison.OrdinalIgnoreCase)))
        {
            if (!rule.NumericValueFrom.HasValue) continue;

            DateOnly threshold = DateOnly.FromDateTime(nowUtc.AddYears(-(int)rule.NumericValueFrom.Value));
            string op = rule.OperatorCode;

            query = op switch
            {
                _ when op.Equals(OperatorCode.GreaterThan.ToString(), StringComparison.OrdinalIgnoreCase)
                    => query.Where(acc => dbContext.Set<Person>().Any(p => p.Id == acc.PersonId && p.DateOfBirth < threshold)),
                _ when op.Equals(OperatorCode.GreaterThanOrEqual.ToString(), StringComparison.OrdinalIgnoreCase)
                    => query.Where(acc => dbContext.Set<Person>().Any(p => p.Id == acc.PersonId && p.DateOfBirth <= threshold)),
                _ when op.Equals(OperatorCode.LessThan.ToString(), StringComparison.OrdinalIgnoreCase)
                    => query.Where(acc => dbContext.Set<Person>().Any(p => p.Id == acc.PersonId && p.DateOfBirth > threshold)),
                _ when op.Equals(OperatorCode.LessThanOrEqual.ToString(), StringComparison.OrdinalIgnoreCase)
                    => query.Where(acc => dbContext.Set<Person>().Any(p => p.Id == acc.PersonId && p.DateOfBirth >= threshold)),
                _ when op.Equals(OperatorCode.Equals.ToString(), StringComparison.OrdinalIgnoreCase)
                    => query.Where(acc => dbContext.Set<Person>().Any(p =>
                        p.Id == acc.PersonId &&
                        p.DateOfBirth <= threshold &&
                        p.DateOfBirth > threshold.AddYears(-1))),
                _ when op.Equals(OperatorCode.NotEquals.ToString(), StringComparison.OrdinalIgnoreCase)
                    => query.Where(acc => dbContext.Set<Person>().Any(p =>
                        p.Id == acc.PersonId &&
                        (p.DateOfBirth > threshold || p.DateOfBirth <= threshold.AddYears(-1)))),
                _ when op.Equals(OperatorCode.Between.ToString(), StringComparison.OrdinalIgnoreCase) && rule.NumericValueTo.HasValue
                    => query.Where(acc => dbContext.Set<Person>().Any(p =>
                        p.Id == acc.PersonId &&
                        p.DateOfBirth <= threshold &&
                        p.DateOfBirth > DateOnly.FromDateTime(nowUtc.AddYears(-(int)rule.NumericValueTo!.Value)).AddYears(-1))),
                _ => query
            };
        }

        return query;
    }

    private IQueryable<EducationAccount> ApplyEnrollmentRules(
        IQueryable<EducationAccount> query,
        IReadOnlyList<CampaignRuleProjection> rules,
        DateTime nowUtc)
    {
        DateOnly nowOnly = DateOnly.FromDateTime(nowUtc);

        foreach (CampaignRuleProjection rule in rules)
        {
            bool isSchoolingStatus = rule.CriterionCode.Equals(TopUpCriterionCode.SchoolingStatus.ToString(), StringComparison.OrdinalIgnoreCase);
            bool isLevel = rule.CriterionCode.Equals(TopUpCriterionCode.Level.ToString(), StringComparison.OrdinalIgnoreCase);
            bool isClass = rule.CriterionCode.Equals(TopUpCriterionCode.Class.ToString(), StringComparison.OrdinalIgnoreCase);

            if (!isSchoolingStatus && !isLevel && !isClass)
                continue;

            string op = rule.OperatorCode;
            bool isEquals = op.Equals(OperatorCode.Equals.ToString(), StringComparison.OrdinalIgnoreCase);
            bool isNotEquals = op.Equals(OperatorCode.NotEquals.ToString(), StringComparison.OrdinalIgnoreCase);
            bool isIn = op.Equals(OperatorCode.In.ToString(), StringComparison.OrdinalIgnoreCase);
            string? text = rule.TextValue;

            if (isSchoolingStatus && isEquals && !string.IsNullOrEmpty(text))
                query = query.Where(acc => dbContext.Set<SchoolEnrollment>().Any(e =>
                    e.PersonId == acc.PersonId && e.SchoolingStatusCode == text &&
                    e.StartDate <= nowOnly && (e.EndDate == null || e.EndDate >= nowOnly)));
            else if (isSchoolingStatus && isNotEquals && !string.IsNullOrEmpty(text))
                query = query.Where(acc => dbContext.Set<SchoolEnrollment>().Any(e =>
                    e.PersonId == acc.PersonId && e.SchoolingStatusCode != text &&
                    e.StartDate <= nowOnly && (e.EndDate == null || e.EndDate >= nowOnly)));
            else if (isSchoolingStatus && isIn && !string.IsNullOrEmpty(text))
            {
                var inValues = System.Text.Json.JsonSerializer.Deserialize<List<string>>(text) ?? [];
                query = query.Where(acc => dbContext.Set<SchoolEnrollment>().Any(e =>
                    e.PersonId == acc.PersonId && inValues.Contains(e.SchoolingStatusCode) &&
                    e.StartDate <= nowOnly && (e.EndDate == null || e.EndDate >= nowOnly)));
            }
            else if (isLevel && isEquals && !string.IsNullOrEmpty(text))
                query = query.Where(acc => dbContext.Set<SchoolEnrollment>().Any(e =>
                    e.PersonId == acc.PersonId && e.LevelCode == text &&
                    e.StartDate <= nowOnly && (e.EndDate == null || e.EndDate >= nowOnly)));
            else if (isLevel && isNotEquals && !string.IsNullOrEmpty(text))
                query = query.Where(acc => dbContext.Set<SchoolEnrollment>().Any(e =>
                    e.PersonId == acc.PersonId && e.LevelCode != text &&
                    e.StartDate <= nowOnly && (e.EndDate == null || e.EndDate >= nowOnly)));
            else if (isLevel && isIn && !string.IsNullOrEmpty(text))
            {
                var inValues = System.Text.Json.JsonSerializer.Deserialize<List<string>>(text) ?? [];
                query = query.Where(acc => dbContext.Set<SchoolEnrollment>().Any(e =>
                    e.PersonId == acc.PersonId && inValues.Contains(e.LevelCode) &&
                    e.StartDate <= nowOnly && (e.EndDate == null || e.EndDate >= nowOnly)));
            }
            else if (isClass && isEquals && !string.IsNullOrEmpty(text))
                query = query.Where(acc => dbContext.Set<SchoolEnrollment>().Any(e =>
                    e.PersonId == acc.PersonId && e.ClassCode == text &&
                    e.StartDate <= nowOnly && (e.EndDate == null || e.EndDate >= nowOnly)));
            else if (isClass && isNotEquals && !string.IsNullOrEmpty(text))
                query = query.Where(acc => dbContext.Set<SchoolEnrollment>().Any(e =>
                    e.PersonId == acc.PersonId && e.ClassCode != text &&
                    e.StartDate <= nowOnly && (e.EndDate == null || e.EndDate >= nowOnly)));
            else if (isClass && isIn && !string.IsNullOrEmpty(text))
            {
                var inValues = System.Text.Json.JsonSerializer.Deserialize<List<string>>(text) ?? [];
                query = query.Where(acc => dbContext.Set<SchoolEnrollment>().Any(e =>
                    e.PersonId == acc.PersonId && e.ClassCode != null && inValues.Contains(e.ClassCode) &&
                    e.StartDate <= nowOnly && (e.EndDate == null || e.EndDate >= nowOnly)));
            }
        }

        return query;
    }

    private static IQueryable<EducationAccount> ApplyBalanceRules(
        IQueryable<EducationAccount> query,
        IReadOnlyList<CampaignRuleProjection> rules)
    {
        foreach (CampaignRuleProjection rule in rules.Where(r =>
            r.CriterionCode.Equals(TopUpCriterionCode.AccountBalance.ToString(), StringComparison.OrdinalIgnoreCase)))
        {
            string op = rule.OperatorCode;

            if (op.Equals(OperatorCode.GreaterThan.ToString(), StringComparison.OrdinalIgnoreCase) && rule.NumericValueFrom.HasValue)
                query = query.Where(x => x.CachedBalance > rule.NumericValueFrom.Value);
            else if (op.Equals(OperatorCode.GreaterThanOrEqual.ToString(), StringComparison.OrdinalIgnoreCase) && rule.NumericValueFrom.HasValue)
                query = query.Where(x => x.CachedBalance >= rule.NumericValueFrom.Value);
            else if (op.Equals(OperatorCode.LessThan.ToString(), StringComparison.OrdinalIgnoreCase) && rule.NumericValueFrom.HasValue)
                query = query.Where(x => x.CachedBalance < rule.NumericValueFrom.Value);
            else if (op.Equals(OperatorCode.LessThanOrEqual.ToString(), StringComparison.OrdinalIgnoreCase) && rule.NumericValueFrom.HasValue)
                query = query.Where(x => x.CachedBalance <= rule.NumericValueFrom.Value);
            else if (op.Equals(OperatorCode.Equals.ToString(), StringComparison.OrdinalIgnoreCase) && rule.NumericValueFrom.HasValue)
                query = query.Where(x => x.CachedBalance == rule.NumericValueFrom.Value);
            else if (op.Equals(OperatorCode.NotEquals.ToString(), StringComparison.OrdinalIgnoreCase) && rule.NumericValueFrom.HasValue)
                query = query.Where(x => x.CachedBalance != rule.NumericValueFrom.Value);
            else if (op.Equals(OperatorCode.Between.ToString(), StringComparison.OrdinalIgnoreCase) && rule.NumericValueFrom.HasValue && rule.NumericValueTo.HasValue)
                query = query.Where(x => x.CachedBalance >= rule.NumericValueFrom.Value && x.CachedBalance <= rule.NumericValueTo.Value);
        }

        return query;
    }

    private IQueryable<EducationAccount> ApplyEducationAccountRules(
        IQueryable<EducationAccount> query,
        IReadOnlyList<CampaignRuleProjection> rules)
    {
        foreach (CampaignRuleProjection rule in rules.Where(r =>
            r.CriterionCode.Equals(TopUpCriterionCode.HasEducationAccount.ToString(), StringComparison.OrdinalIgnoreCase)))
        {
            bool wantsAccount = string.Equals(rule.TextValue, "YES", StringComparison.OrdinalIgnoreCase);

            // The base query already contains active education accounts; "NO" cannot yield top-up recipients.
            if (!wantsAccount)
                query = query.Where(_ => false);
        }

        return query;
    }
}
