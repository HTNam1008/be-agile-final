namespace Moe.Modules.FasPayment.Application.Audit;

internal interface IFasSchoolAuditResolver
{
    Task<IReadOnlyCollection<long>> ResolveFromCourseIdsAsync(
        IReadOnlyCollection<long> courseIds,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<long>> ResolveForSchemeAsync(
        long schemeId,
        CancellationToken cancellationToken);
}
