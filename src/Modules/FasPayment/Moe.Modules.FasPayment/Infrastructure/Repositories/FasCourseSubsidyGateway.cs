using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Moe.Modules.CourseBilling.IGateway.Fas;
using Moe.Modules.FasPayment.Domain.Fas;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.FasPayment.Infrastructure.Repositories;

internal sealed class FasCourseSubsidyGateway(MoeDbContext dbContext) : IFasCourseSubsidyGateway
{
    public async Task<IReadOnlyCollection<CourseFasSubsidy>> ListEligibleSubsidiesAsync(
        long personId,
        long courseId,
        DateOnly enrolledDate,
        IReadOnlyCollection<long>? fasApplicationSchemeIds,
        CancellationToken cancellationToken)
    {
        long[] selectedIds = fasApplicationSchemeIds?.Where(id => id > 0).Distinct().ToArray() ?? [];
        if (selectedIds.Length == 0)
        {
            return [];
        }

        var rows = await (
            from item in dbContext.Set<FasApplicationScheme>().AsNoTracking()
            join application in dbContext.Set<FasApplication>().AsNoTracking()
                on item.FasApplicationId equals application.Id
            join scheme in dbContext.Set<FasScheme>().AsNoTracking()
                on item.FasSchemeId equals scheme.Id
            join activeScheme in dbContext.Set<FasActiveScheme>().AsNoTracking()
                on item.Id equals activeScheme.FasApplicationSchemeId
            where application.StudentPersonId == personId
                  && selectedIds.Contains(item.Id)
                  && item.StatusCode == "APPROVED"
                  && item.IsActive
                  && activeScheme.StudentPersonId == personId
                  && activeScheme.StatusCode == "ACTIVE"
                  && (item.ValidFrom ?? enrolledDate) <= enrolledDate
                  && (item.ValidTo ?? enrolledDate) >= enrolledDate
                  && activeScheme.ActiveFrom <= enrolledDate
                  && activeScheme.ActiveTo >= enrolledDate
                  && scheme.StatusCode == "ACTIVE"
                  && !dbContext.Set<FasVoucherRedemption>().Any(redemption =>
                      redemption.FasApplicationSchemeId == item.Id &&
                      redemption.StatusCode != "CANCELLED" &&
                      redemption.CourseId != courseId)
                  && (
                      !dbContext.Set<FasSchemeCourse>().Any(schemeCourse =>
                          schemeCourse.FasSchemeId == item.FasSchemeId)
                      || dbContext.Set<FasSchemeCourse>().Any(schemeCourse =>
                          schemeCourse.FasSchemeId == item.FasSchemeId &&
                          schemeCourse.CourseId == courseId))
            orderby item.ApprovedAtUtc, item.Id
            select new
            {
                item.Id,
                item.ApprovedComponentsJson,
                item.ApprovedAmount
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => ToCourseFasSubsidy(row.Id, row.ApprovedComponentsJson, row.ApprovedAmount))
            .Where(subsidy => subsidy is not null)
            .Select(subsidy => subsidy!)
            .ToArray();
    }

    public async Task RecordPendingRedemptionsAsync(
        long personId,
        long courseId,
        long courseEnrollmentId,
        long billId,
        decimal totalSubsidyAmount,
        IReadOnlyCollection<CourseFasSubsidy> subsidies,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        long[] selectedIds = subsidies.Select(x => x.FasApplicationSchemeId).Distinct().ToArray();
        FasVoucherRedemption[] existingPending = await dbContext.Set<FasVoucherRedemption>()
            .Where(x => x.CourseEnrollmentId == courseEnrollmentId && x.StatusCode == "PENDING")
            .ToArrayAsync(cancellationToken);

        foreach (FasVoucherRedemption redemption in existingPending)
        {
            redemption.Cancel();
        }

        if (existingPending.Length > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        if (selectedIds.Length == 0 || totalSubsidyAmount <= 0m)
        {
            return;
        }

        bool hasConflictingRedemption = await dbContext.Set<FasVoucherRedemption>()
            .AnyAsync(
                x => selectedIds.Contains(x.FasApplicationSchemeId)
                     && x.StatusCode != "CANCELLED"
                     && x.CourseEnrollmentId != courseEnrollmentId,
                cancellationToken);
        if (hasConflictingRedemption)
        {
            throw new InvalidOperationException("One or more selected FAS vouchers are already reserved or redeemed.");
        }

        decimal[] allocations = AllocateEvenly(totalSubsidyAmount, selectedIds.Length);
        for (int index = 0; index < selectedIds.Length; index++)
        {
            await dbContext.Set<FasVoucherRedemption>().AddAsync(
                FasVoucherRedemption.Pending(
                    personId,
                    selectedIds[index],
                    courseId,
                    courseEnrollmentId,
                    billId,
                    allocations[index],
                    utcNow),
                cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task CancelPendingRedemptionsForEnrollmentAsync(
        long courseEnrollmentId,
        DateTime cancelledAtUtc,
        CancellationToken cancellationToken)
    {
        FasVoucherRedemption[] redemptions = await dbContext.Set<FasVoucherRedemption>()
            .Where(x => x.CourseEnrollmentId == courseEnrollmentId && x.StatusCode == "PENDING")
            .ToArrayAsync(cancellationToken);
        if (redemptions.Length == 0)
        {
            return;
        }

        foreach (FasVoucherRedemption redemption in redemptions)
        {
            redemption.Cancel();
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RedeemPendingRedemptionsForBillsAsync(
        IReadOnlyCollection<long> billIds,
        DateTime redeemedAtUtc,
        CancellationToken cancellationToken)
    {
        long[] ids = billIds.Where(id => id > 0).Distinct().ToArray();
        if (ids.Length == 0)
        {
            return;
        }

        FasVoucherRedemption[] redemptions = await dbContext.Set<FasVoucherRedemption>()
            .Where(x => ids.Contains(x.BillId) && x.StatusCode == "PENDING")
            .ToArrayAsync(cancellationToken);
        if (redemptions.Length == 0)
        {
            return;
        }

        long[] applicationSchemeIds = redemptions.Select(x => x.FasApplicationSchemeId).Distinct().ToArray();
        FasApplicationScheme[] schemes = await dbContext.Set<FasApplicationScheme>()
            .Where(x => applicationSchemeIds.Contains(x.Id))
            .ToArrayAsync(cancellationToken);
        FasActiveScheme[] activeSchemes = await dbContext.Set<FasActiveScheme>()
            .Where(x => applicationSchemeIds.Contains(x.FasApplicationSchemeId) && x.StatusCode == "ACTIVE")
            .ToArrayAsync(cancellationToken);

        foreach (FasVoucherRedemption redemption in redemptions)
        {
            redemption.Redeem(redeemedAtUtc);
        }

        foreach (FasApplicationScheme scheme in schemes)
        {
            string oldStatus = scheme.StatusCode;
            scheme.Redeem(redeemedAtUtc);
            dbContext.Add(FasStatusHistory.Create(
                scheme.FasApplicationId,
                scheme.Id,
                oldStatus,
                "REDEEMED",
                "Redeemed after course bill payment was confirmed.",
                0,
                "SYSTEM",
                redeemedAtUtc));
        }

        foreach (FasActiveScheme activeScheme in activeSchemes)
        {
            activeScheme.Deactivate(0, redeemedAtUtc, "REDEEMED");
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static CourseFasSubsidy? ToCourseFasSubsidy(long applicationSchemeId, string? approvedComponentsJson, decimal? approvedAmount)
    {
        ApprovedFasComponents? components = ParseApprovedComponents(approvedComponentsJson);
        string? type = components?.SubsidyType;
        decimal? value = components?.SubsidyValue ?? approvedAmount;

        if (string.IsNullOrWhiteSpace(type) || value is null || value.Value <= 0m)
        {
            return null;
        }

        string normalized = type.Trim().ToUpperInvariant();
        return normalized is "PERCENTAGE" or "FIXED"
            ? new CourseFasSubsidy(applicationSchemeId, normalized, value.Value)
            : null;
    }

    private static decimal[] AllocateEvenly(decimal totalAmount, int count)
    {
        if (count <= 0)
        {
            return [];
        }

        long totalMinor = decimal.ToInt64(decimal.Round(totalAmount, 2, MidpointRounding.AwayFromZero) * 100m);
        long baseMinor = totalMinor / count;
        long remainderMinor = totalMinor % count;
        return Enumerable.Range(1, count)
            .Select(sequence => (baseMinor + (sequence <= remainderMinor ? 1 : 0)) / 100m)
            .ToArray();
    }

    private static ApprovedFasComponents? ParseApprovedComponents(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<ApprovedFasComponents>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed record ApprovedFasComponents(string? SubsidyType, decimal? SubsidyValue);
}
