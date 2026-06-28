using Microsoft.EntityFrameworkCore;
using Moe.Infrastructure.Shared.Api;
using Moe.Modules.CourseBilling.Domain.Billing;
using Moe.Modules.CourseBilling.Domain.Courses;
using Moe.Modules.CourseBilling.IGateway.Repositories;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.CourseBilling.Infrastructure.Repositories;

internal sealed class DeferExtensionRequestRepository(MoeDbContext dbContext)
    : IDeferExtensionRequestRepository
{
    public Task<DeferExtensionBillSnapshot?> FindBillForStudentAsync(
        long billId,
        long personId,
        CancellationToken cancellationToken)
        => (
            from bill in dbContext.Set<Bill>().AsNoTracking()
            join enrollment in dbContext.Set<CourseEnrollment>().AsNoTracking()
                on bill.CourseEnrollmentId equals enrollment.Id
            join course in dbContext.Set<Course>().AsNoTracking()
                on enrollment.CourseId equals course.Id
            where bill.Id == billId && enrollment.PersonId == personId
            select new DeferExtensionBillSnapshot(
                bill.Id,
                bill.BillNumber,
                enrollment.Id,
                enrollment.PersonId,
                course.OrganizationId,
                course.Id,
                course.CourseCode,
                course.CourseName,
                bill.DeferralCount,
                bill.IsDeferExtensionGranted,
                bill.OutstandingAmount,
                bill.BillStatusCode))
        .SingleOrDefaultAsync(cancellationToken);

    public Task<bool> HasPendingRequestAsync(long billId, CancellationToken cancellationToken)
        => dbContext.Set<DeferExtensionRequest>()
            .AnyAsync(
                request => request.BillId == billId &&
                    request.StatusCode == DeferExtensionRequestStatusCodes.Pending,
                cancellationToken);

    public async Task AddAsync(DeferExtensionRequest request, CancellationToken cancellationToken)
        => await dbContext.Set<DeferExtensionRequest>().AddAsync(request, cancellationToken);

    public async Task<DeferExtensionReviewAggregate?> FindForReviewAsync(
        long requestId,
        CancellationToken cancellationToken)
    {
        DeferExtensionRequest? request = await dbContext.Set<DeferExtensionRequest>()
            .SingleOrDefaultAsync(candidate => candidate.Id == requestId, cancellationToken);
        if (request is null)
        {
            return null;
        }

        Bill bill = await dbContext.Set<Bill>()
            .SingleAsync(candidate => candidate.Id == request.BillId, cancellationToken);
        return new DeferExtensionReviewAggregate(request, bill);
    }

    public async Task<PageResponse<DeferExtensionRequestProjection>> ListAsync(
        long? organizationId,
        IReadOnlyCollection<long> scopedOrganizationIds,
        bool hasGlobalAccess,
        string? statusCode,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        int normalizedPage = Math.Max(1, page);
        int normalizedPageSize = Math.Clamp(pageSize, 1, 100);
        IQueryable<DeferExtensionRequestProjection> query =
            from request in dbContext.Set<DeferExtensionRequest>().AsNoTracking()
            join bill in dbContext.Set<Bill>().AsNoTracking() on request.BillId equals bill.Id
            join enrollment in dbContext.Set<CourseEnrollment>().AsNoTracking()
                on request.CourseEnrollmentId equals enrollment.Id
            join course in dbContext.Set<Course>().AsNoTracking() on enrollment.CourseId equals course.Id
            select new DeferExtensionRequestProjection(
                request.Id,
                request.BillId,
                request.CourseEnrollmentId,
                request.PersonId,
                request.OrganizationId,
                request.StatusCode,
                request.RequestedAtUtc,
                request.RequestedByLoginAccountId,
                request.ReviewedAtUtc,
                request.ReviewedByLoginAccountId,
                request.DeadlineAtUtc,
                course.CourseCode,
                course.CourseName,
                bill.BillNumber,
                bill.DeferralCount);

        if (organizationId is long selectedOrganizationId)
        {
            query = query.Where(request => request.OrganizationId == selectedOrganizationId);
        }
        else if (!hasGlobalAccess)
        {
            query = query.Where(request => scopedOrganizationIds.Contains(request.OrganizationId));
        }

        if (!string.IsNullOrWhiteSpace(statusCode))
        {
            string normalizedStatus = statusCode.Trim().ToUpperInvariant();
            query = query.Where(request => request.StatusCode == normalizedStatus);
        }

        int total = await query.CountAsync(cancellationToken);
        DeferExtensionRequestProjection[] items = await query
            .OrderByDescending(request => request.RequestedAtUtc)
            .ThenByDescending(request => request.RequestId)
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToArrayAsync(cancellationToken);

        return new PageResponse<DeferExtensionRequestProjection>(
            items,
            normalizedPage,
            normalizedPageSize,
            total);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
        => dbContext.SaveChangesAsync(cancellationToken);
}
