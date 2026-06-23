namespace Moe.Modules.FasPayment.Application.Webhooks;

internal interface IStripeWebhookCoordinator
{
    Task<T> ExecuteAsync<T>(long checkoutId, Func<Task<T>> operation, CancellationToken cancellationToken);
}

internal sealed class StripeWebhookCoordinator : IStripeWebhookCoordinator
{
    private readonly SemaphoreSlim[] stripes = Enumerable.Range(0, 64)
        .Select(_ => new SemaphoreSlim(1, 1))
        .ToArray();

    public async Task<T> ExecuteAsync<T>(
        long checkoutId,
        Func<Task<T>> operation,
        CancellationToken cancellationToken)
    {
        int index = (int)((ulong)checkoutId % (ulong)stripes.Length);
        SemaphoreSlim gate = stripes[index];
        await gate.WaitAsync(cancellationToken);
        try
        {
            return await operation();
        }
        finally
        {
            gate.Release();
        }
    }
}
