namespace Moe.Modules.EducationAccountTopUp.IGateway.TopUps;

internal interface ITopUpAccountProjectionRepository
{
    Task<IReadOnlyCollection<long>> FindMatchingPersonIdsAsync(
        TopUpAccountSearchCriteria criteria,
        CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<long, TopUpAccountProjection>> FindByPersonIdsAsync(
        IReadOnlyCollection<long> personIds,
        CancellationToken cancellationToken);
}
