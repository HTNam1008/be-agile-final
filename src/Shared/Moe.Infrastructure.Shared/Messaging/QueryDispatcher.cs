using Microsoft.Extensions.DependencyInjection;
using Moe.Application.Abstractions.Messaging;
using Moe.SharedKernel.Results;

namespace Moe.Infrastructure.Shared.Messaging;

internal sealed class QueryDispatcher(IServiceProvider serviceProvider) : IQueryDispatcher
{
    public Task<Result<TResponse>> Send<TResponse>(
        IQuery<TResponse> query,
        CancellationToken cancellationToken)
    {
        Type handlerType = typeof(IQueryHandler<,>).MakeGenericType(query.GetType(), typeof(TResponse));
        object handler = serviceProvider.GetRequiredService(handlerType);
        object? result = handlerType
            .GetMethod(nameof(IQueryHandler<IQuery<TResponse>, TResponse>.Handle))!
            .Invoke(handler, [query, cancellationToken]);

        return (Task<Result<TResponse>>)result!;
    }
}
