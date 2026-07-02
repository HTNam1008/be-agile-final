using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moe.Application.Abstractions.Clock;
using Moe.Modules.CourseBilling.Domain.Billing;
using Moe.Modules.CourseBilling.Domain.Courses;
using Moe.Modules.CourseBilling.Infrastructure;
using Moe.Modules.CourseBilling.IGateway.Repositories;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.CourseBilling.Infrastructure.BillingStatements;

internal sealed class MonthlyBillNotificationWorker(
    IServiceScopeFactory scopeFactory,
    IClock clock,
    ILogger<MonthlyBillNotificationWorker> logger,
    IOptions<CourseBillingWorkerOptions> options) : BackgroundService
{
    private DateOnly? _lastProcessedMonthStart;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunIfDueAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Monthly bill notification worker failed.");
            }

            try
            {
                await Task.Delay(GetPollInterval(), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private TimeSpan GetPollInterval()
        => TimeSpan.FromSeconds(Math.Clamp(options.Value.MonthlyBillNotificationPollIntervalSeconds, 1, 86400));

    internal async Task RunIfDueAsync(CancellationToken cancellationToken)
    {
        DateTime utcNow = clock.UtcNow.UtcDateTime;
        DateOnly today = DateOnly.FromDateTime(utcNow);
        if (today.Day != 1)
        {
            return;
        }

        DateOnly monthStart = new(today.Year, today.Month, 1);
        if (_lastProcessedMonthStart == monthStart)
        {
            return;
        }

        using IServiceScope scope = scopeFactory.CreateScope();
        MoeDbContext dbContext = scope.ServiceProvider.GetRequiredService<MoeDbContext>();
        IBillingStatementRepository statements = scope.ServiceProvider.GetRequiredService<IBillingStatementRepository>();

        long[] personIds = await GetPersonIdsWithOutstandingBillsAsync(
            dbContext,
            monthStart,
            cancellationToken);

        logger.LogInformation(
            "Monthly bill notification worker found {PersonCount} account holders for {BillingMonth}.",
            personIds.Length,
            monthStart);

        foreach (long personId in personIds)
        {
            try
            {
                await statements.GetOrCreateAsync(
                    personId,
                    monthStart.Year,
                    monthStart.Month,
                    utcNow,
                    BillingStatementNotificationMode.SendMonthlyBill,
                    cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(
                    ex,
                    "Monthly bill notification failed for account holder. PersonId={PersonId} BillingMonth={BillingMonth}",
                    personId,
                    monthStart);
            }
        }

        _lastProcessedMonthStart = monthStart;
    }

    private static Task<long[]> GetPersonIdsWithOutstandingBillsAsync(
        MoeDbContext dbContext,
        DateOnly monthStart,
        CancellationToken cancellationToken)
    {
        DateOnly monthEnd = monthStart.AddMonths(1);

        return (
            from bill in dbContext.Set<Bill>().AsNoTracking()
            join enrollment in dbContext.Set<CourseEnrollment>().AsNoTracking()
                on bill.CourseEnrollmentId equals enrollment.Id
            where enrollment.EnrollmentStatusCode != CourseEnrollmentStatusCodes.PendingPlanSelection
                && bill.CurrentDueDate >= monthStart
                && bill.CurrentDueDate < monthEnd
                && bill.OutstandingAmount > 0m
                && bill.BillStatusCode != BillStatusCodes.Paid
                && bill.BillStatusCode != BillStatusCodes.Cancelled
            select enrollment.PersonId)
            .Distinct()
            .OrderBy(personId => personId)
            .ToArrayAsync(cancellationToken);
    }
}

