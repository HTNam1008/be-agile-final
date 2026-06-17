namespace Moe.Modules.EducationAccountTopUp.IGateway;

public interface ITopUpRunDispatcher
{
    Task EnqueueAsync(long topUpRunId, CancellationToken cancellationToken = default);
}
