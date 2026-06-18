using Microsoft.EntityFrameworkCore;
using Moe.Modules.EducationAccountTopUp.Contracts.TopUps.Enums;
using Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;
using Moe.Modules.EducationAccountTopUp.Domain.TopUps;
using Moe.Modules.IdentityPlatform.Domain.People;
using Moe.Modules.IdentityPlatform.Domain.Schooling;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.EducationAccountTopUp.Application.TopUps;

internal static class DynamicRuleEvaluator
{
    public static IQueryable<EducationAccount> ApplyRules(
        MoeDbContext dbContext,
        IQueryable<EducationAccount> baseQuery,
        List<TopUpCampaignRule> rules,
        DateTime nowUtc)
    {
        var query = baseQuery;
        
        bool requiresPerson = rules.Any(r => r.CriterionCode.Equals(TopUpCriterionCode.Age.ToString(), StringComparison.OrdinalIgnoreCase));
        bool requiresEnrollment = rules.Any(r => r.CriterionCode.Equals(TopUpCriterionCode.SchoolingStatus.ToString(), StringComparison.OrdinalIgnoreCase) ||
                                                 r.CriterionCode.Equals(TopUpCriterionCode.Level.ToString(), StringComparison.OrdinalIgnoreCase) ||
                                                 r.CriterionCode.Equals(TopUpCriterionCode.Class.ToString(), StringComparison.OrdinalIgnoreCase));

        if (requiresPerson)
        {
            foreach (var rule in rules.Where(r => r.CriterionCode.Equals(TopUpCriterionCode.Age.ToString(), StringComparison.OrdinalIgnoreCase)))
            {
                if (rule.NumericValueFrom.HasValue)
                {
                    // Fix Leap Year Bug: safely subtract years
                    var thresholdDate = DateOnly.FromDateTime(nowUtc.AddYears(-(int)rule.NumericValueFrom.Value));
                    
                    if (rule.OperatorCode.Equals(OperatorCode.GreaterThan.ToString(), StringComparison.OrdinalIgnoreCase))
                    {
                        query = query.Where(acc => dbContext.Set<Person>().Any(p => p.Id == acc.PersonId && p.DateOfBirth < thresholdDate));
                    }
                    else if (rule.OperatorCode.Equals(OperatorCode.GreaterThanOrEqual.ToString(), StringComparison.OrdinalIgnoreCase))
                    {
                        query = query.Where(acc => dbContext.Set<Person>().Any(p => p.Id == acc.PersonId && p.DateOfBirth <= thresholdDate));
                    }
                    else if (rule.OperatorCode.Equals(OperatorCode.LessThan.ToString(), StringComparison.OrdinalIgnoreCase))
                    {
                        query = query.Where(acc => dbContext.Set<Person>().Any(p => p.Id == acc.PersonId && p.DateOfBirth > thresholdDate));
                    }
                    else if (rule.OperatorCode.Equals(OperatorCode.LessThanOrEqual.ToString(), StringComparison.OrdinalIgnoreCase))
                    {
                        query = query.Where(acc => dbContext.Set<Person>().Any(p => p.Id == acc.PersonId && p.DateOfBirth >= thresholdDate));
                    }
                    else if (rule.OperatorCode.Equals(OperatorCode.Equals.ToString(), StringComparison.OrdinalIgnoreCase))
                    {
                        var thresholdDateNext = thresholdDate.AddYears(-1);
                        query = query.Where(acc => dbContext.Set<Person>().Any(p => p.Id == acc.PersonId && p.DateOfBirth <= thresholdDate && p.DateOfBirth > thresholdDateNext));
                    }
                    else if (rule.OperatorCode.Equals(OperatorCode.NotEquals.ToString(), StringComparison.OrdinalIgnoreCase))
                    {
                        var thresholdDateNext = thresholdDate.AddYears(-1);
                        query = query.Where(acc => dbContext.Set<Person>().Any(p => p.Id == acc.PersonId && (p.DateOfBirth > thresholdDate || p.DateOfBirth <= thresholdDateNext)));
                    }
                    else if (rule.OperatorCode.Equals(OperatorCode.Between.ToString(), StringComparison.OrdinalIgnoreCase) && rule.NumericValueTo.HasValue)
                    {
                        var thresholdToDate = DateOnly.FromDateTime(nowUtc.AddYears(-(int)rule.NumericValueTo.Value)).AddYears(-1);
                        query = query.Where(acc => dbContext.Set<Person>().Any(p => p.Id == acc.PersonId && p.DateOfBirth <= thresholdDate && p.DateOfBirth > thresholdToDate));
                    }
                }
            }
        }

        if (requiresEnrollment)
        {
            var nowOnly = DateOnly.FromDateTime(nowUtc);
            foreach (var rule in rules)
            {
                if (rule.CriterionCode.Equals(TopUpCriterionCode.SchoolingStatus.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    if (rule.OperatorCode.Equals(OperatorCode.Equals.ToString(), StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(rule.TextValue))
                    {
                        query = query.Where(acc => dbContext.Set<SchoolEnrollment>().Any(e => e.PersonId == acc.PersonId && e.SchoolingStatusCode == rule.TextValue && e.StartDate <= nowOnly && (e.EndDate == null || e.EndDate >= nowOnly)));
                    }
                    else if (rule.OperatorCode.Equals(OperatorCode.NotEquals.ToString(), StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(rule.TextValue))
                    {
                        query = query.Where(acc => dbContext.Set<SchoolEnrollment>().Any(e => e.PersonId == acc.PersonId && e.SchoolingStatusCode != rule.TextValue && e.StartDate <= nowOnly && (e.EndDate == null || e.EndDate >= nowOnly)));
                    }
                    else if (rule.OperatorCode.Equals(OperatorCode.In.ToString(), StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(rule.TextValue))
                    {
                        var inValues = System.Text.Json.JsonSerializer.Deserialize<List<string>>(rule.TextValue) ?? new List<string>();
                        query = query.Where(acc => dbContext.Set<SchoolEnrollment>().Any(e => e.PersonId == acc.PersonId && inValues.Contains(e.SchoolingStatusCode) && e.StartDate <= nowOnly && (e.EndDate == null || e.EndDate >= nowOnly)));
                    }
                }
                else if (rule.CriterionCode.Equals(TopUpCriterionCode.Level.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    if (rule.OperatorCode.Equals(OperatorCode.Equals.ToString(), StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(rule.TextValue))
                    {
                        query = query.Where(acc => dbContext.Set<SchoolEnrollment>().Any(e => e.PersonId == acc.PersonId && e.LevelCode == rule.TextValue && e.StartDate <= nowOnly && (e.EndDate == null || e.EndDate >= nowOnly)));
                    }
                    else if (rule.OperatorCode.Equals(OperatorCode.NotEquals.ToString(), StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(rule.TextValue))
                    {
                        query = query.Where(acc => dbContext.Set<SchoolEnrollment>().Any(e => e.PersonId == acc.PersonId && e.LevelCode != rule.TextValue && e.StartDate <= nowOnly && (e.EndDate == null || e.EndDate >= nowOnly)));
                    }
                    else if (rule.OperatorCode.Equals(OperatorCode.In.ToString(), StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(rule.TextValue))
                    {
                        var inValues = System.Text.Json.JsonSerializer.Deserialize<List<string>>(rule.TextValue) ?? new List<string>();
                        query = query.Where(acc => dbContext.Set<SchoolEnrollment>().Any(e => e.PersonId == acc.PersonId && inValues.Contains(e.LevelCode) && e.StartDate <= nowOnly && (e.EndDate == null || e.EndDate >= nowOnly)));
                    }
                }
                else if (rule.CriterionCode.Equals(TopUpCriterionCode.Class.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    if (rule.OperatorCode.Equals(OperatorCode.Equals.ToString(), StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(rule.TextValue))
                    {
                        query = query.Where(acc => dbContext.Set<SchoolEnrollment>().Any(e => e.PersonId == acc.PersonId && e.ClassCode == rule.TextValue && e.StartDate <= nowOnly && (e.EndDate == null || e.EndDate >= nowOnly)));
                    }
                    else if (rule.OperatorCode.Equals(OperatorCode.NotEquals.ToString(), StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(rule.TextValue))
                    {
                        query = query.Where(acc => dbContext.Set<SchoolEnrollment>().Any(e => e.PersonId == acc.PersonId && e.ClassCode != rule.TextValue && e.StartDate <= nowOnly && (e.EndDate == null || e.EndDate >= nowOnly)));
                    }
                    else if (rule.OperatorCode.Equals(OperatorCode.In.ToString(), StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(rule.TextValue))
                    {
                        var inValues = System.Text.Json.JsonSerializer.Deserialize<List<string>>(rule.TextValue) ?? new List<string>();
                        query = query.Where(acc => dbContext.Set<SchoolEnrollment>().Any(e => e.PersonId == acc.PersonId && inValues.Contains(e.ClassCode) && e.StartDate <= nowOnly && (e.EndDate == null || e.EndDate >= nowOnly)));
                    }
                }
            }
        }

        foreach (var rule in rules.Where(r => r.CriterionCode.Equals(TopUpCriterionCode.AccountBalance.ToString(), StringComparison.OrdinalIgnoreCase)))
        {
            if (rule.OperatorCode.Equals(OperatorCode.GreaterThan.ToString(), StringComparison.OrdinalIgnoreCase) && rule.NumericValueFrom.HasValue)
                query = query.Where(x => x.CachedBalance > rule.NumericValueFrom.Value);
            else if (rule.OperatorCode.Equals(OperatorCode.GreaterThanOrEqual.ToString(), StringComparison.OrdinalIgnoreCase) && rule.NumericValueFrom.HasValue)
                query = query.Where(x => x.CachedBalance >= rule.NumericValueFrom.Value);
            else if (rule.OperatorCode.Equals(OperatorCode.LessThan.ToString(), StringComparison.OrdinalIgnoreCase) && rule.NumericValueFrom.HasValue)
                query = query.Where(x => x.CachedBalance < rule.NumericValueFrom.Value);
            else if (rule.OperatorCode.Equals(OperatorCode.LessThanOrEqual.ToString(), StringComparison.OrdinalIgnoreCase) && rule.NumericValueFrom.HasValue)
                query = query.Where(x => x.CachedBalance <= rule.NumericValueFrom.Value);
            else if (rule.OperatorCode.Equals(OperatorCode.Equals.ToString(), StringComparison.OrdinalIgnoreCase) && rule.NumericValueFrom.HasValue)
                query = query.Where(x => x.CachedBalance == rule.NumericValueFrom.Value);
            else if (rule.OperatorCode.Equals(OperatorCode.NotEquals.ToString(), StringComparison.OrdinalIgnoreCase) && rule.NumericValueFrom.HasValue)
                query = query.Where(x => x.CachedBalance != rule.NumericValueFrom.Value);
            else if (rule.OperatorCode.Equals(OperatorCode.Between.ToString(), StringComparison.OrdinalIgnoreCase) && rule.NumericValueFrom.HasValue && rule.NumericValueTo.HasValue)
                query = query.Where(x => x.CachedBalance >= rule.NumericValueFrom.Value && x.CachedBalance <= rule.NumericValueTo.Value);
        }

        return query;
    }
}
