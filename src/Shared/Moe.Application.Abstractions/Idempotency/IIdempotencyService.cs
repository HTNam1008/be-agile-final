namespace Moe.Application.Abstractions.Idempotency;

public interface IIdempotencyService
{
    Task<bool> TryStartAsync(string scope, string key, CancellationToken cancellationToken);
    Task CompleteAsync(string scope, string key, CancellationToken cancellationToken);
}
