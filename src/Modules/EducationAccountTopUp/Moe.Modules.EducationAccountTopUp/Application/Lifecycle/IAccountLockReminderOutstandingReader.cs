namespace Moe.Modules.EducationAccountTopUp.Application.Lifecycle;

internal interface IAccountLockReminderOutstandingReader
{
    Task<decimal?> FindOutstandingAmountAsync(long personId, CancellationToken cancellationToken);
}

