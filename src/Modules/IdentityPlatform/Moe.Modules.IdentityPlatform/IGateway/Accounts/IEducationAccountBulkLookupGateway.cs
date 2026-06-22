namespace Moe.Modules.IdentityPlatform.IGateway.Accounts;

public interface IEducationAccountBulkLookupGateway
{
    Task<IReadOnlyDictionary<long, EducationAccountLookupSummary>> FindByPersonIdsAsync(
        IReadOnlyCollection<long> personIds,
        CancellationToken cancellationToken);
}
