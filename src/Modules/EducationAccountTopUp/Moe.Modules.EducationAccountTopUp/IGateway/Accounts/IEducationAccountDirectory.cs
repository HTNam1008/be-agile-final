namespace Moe.Modules.EducationAccountTopUp.IGateway.Accounts;

public interface IEducationAccountDirectory
{
    Task<EducationAccountSummary?> FindByPersonIdAsync(long personId, CancellationToken cancellationToken);
}
