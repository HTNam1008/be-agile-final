using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Moe.Modules.FasPayment.Application.Applications.GetApplicationDetail;
using Moe.Modules.FasPayment.Application.Applications.GetSchemeApplications;
using Moe.Modules.FasPayment.Domain.Fas;
using Moe.Modules.FasPayment.IGateway.Repositories;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.FasPayment.Infrastructure.Repositories;

internal sealed class FasApplicationRepository(MoeDbContext dbContext) : IFasApplicationRepository
{
    public Task<FasApplication?> FindAsync(long applicationId, CancellationToken cancellationToken = default)
    {
        return dbContext.Set<FasApplication>()
            .SingleOrDefaultAsync(x => x.Id == applicationId, cancellationToken);
    }

    public async Task AddAsync(FasApplication application, CancellationToken cancellationToken = default)
    {
        await dbContext.Set<FasApplication>().AddAsync(application, cancellationToken);
    }

    public async Task AddDecisionAsync(FasApplicationReviewDecision decision, CancellationToken cancellationToken = default)
    {
        await dbContext.Set<FasApplicationReviewDecision>().AddAsync(decision, cancellationToken);
    }

    public async Task<GetSchemeApplicationsResponse?> GetSchemeApplicationsAsync(long schemeId, CancellationToken cancellationToken = default)
    {
        var schemeExists = await dbContext.Set<FasScheme>().AnyAsync(x => x.Id == schemeId, cancellationToken);
        if (!schemeExists) return null;

        var statusCounts = await dbContext.Set<FasApplication>()
            .Where(x => x.FasSchemeId == schemeId)
            .GroupBy(x => x.StatusCode)
            .Select(g => new { StatusCode = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.StatusCode, x => x.Count, cancellationToken);

        int pendingCount = statusCounts.GetValueOrDefault(FasApplicationStatuses.PendingReview, 0);
        int approvedCount = statusCounts.GetValueOrDefault(FasApplicationStatuses.Approved, 0);
        int rejectedCount = statusCounts.GetValueOrDefault(FasApplicationStatuses.Rejected, 0);

        var summary = new SchemeApplicationsSummary(pendingCount, approvedCount, rejectedCount);

        var dbItems = await dbContext.Set<FasApplication>()
            .Where(x => x.FasSchemeId == schemeId)
            .Select(x => new { x.Id, x.ApplicationNo, x.StudentName, x.StudentId, x.SubmittedDate, x.StatusCode })
            .ToListAsync(cancellationToken);

        var items = dbItems.Select(x => new SchemeApplicationItem(
            x.Id,
            x.ApplicationNo,
            x.StudentName,
            x.StudentId,
            x.SubmittedDate.ToString("yyyy-MM-dd"),
            x.StatusCode
        )).ToList();

        return new GetSchemeApplicationsResponse(summary, items);
    }

    public async Task<GetApplicationDetailResponse?> GetApplicationDetailAsync(long applicationId, CancellationToken cancellationToken = default)
    {
        var application = await dbContext.Set<FasApplication>().FirstOrDefaultAsync(x => x.Id == applicationId, cancellationToken);
        if (application == null) return null;

        var scheme = await dbContext.Set<FasScheme>().FirstOrDefaultAsync(x => x.Id == application.FasSchemeId, cancellationToken);
        var decisions = await dbContext.Set<FasApplicationReviewDecision>()
            .Where(x => x.ApplicationId == applicationId)
            .OrderBy(x => x.ReviewedAt)
            .ToListAsync(cancellationToken);

        var schemeDto = new ApplicationDetailScheme(scheme?.Id ?? 0, scheme?.SchemeName ?? "Unknown");
        var decisionDtos = decisions.Select(x => new ApplicationDetailDecision(
            x.Decision,
            x.ReviewerUserId,
            x.ReviewedAt,
            x.Remarks
        )).ToList();

        return new GetApplicationDetailResponse(
            application.Id,
            application.ApplicationNo,
            application.StudentId,
            application.StudentName,
            application.SubmittedDate.ToString("yyyy-MM-dd"),
            application.StatusCode,
            schemeDto,
            decisionDtos
        );
    }
}
