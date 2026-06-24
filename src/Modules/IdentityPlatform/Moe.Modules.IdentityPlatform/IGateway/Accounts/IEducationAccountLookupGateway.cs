namespace Moe.Modules.IdentityPlatform.IGateway.Accounts;

public interface IEducationAccountLookupGateway
{
    Task<EducationAccountLookupSummary?> FindByPersonIdAsync(long personId, CancellationToken cancellationToken);
}
