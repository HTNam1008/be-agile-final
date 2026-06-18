using Microsoft.Extensions.DependencyInjection;
using Moe.Application.Abstractions.Messaging;
using Moe.SharedKernel.Results;

namespace Moe.Infrastructure.Shared.Messaging;

internal sealed class CommandDispatcher(IServiceProvider serviceProvider) : ICommandDispatcher
{
    public Task<Result<TResponse>> Send<TResponse>(
        ICommand<TResponse> command,
        CancellationToken cancellationToken)
    {
        Type handlerType = typeof(ICommandHandler<,>).MakeGenericType(command.GetType(), typeof(TResponse));
        object handler = serviceProvider.GetRequiredService(handlerType);
        object? result = handlerType
            .GetMethod(nameof(ICommandHandler<ICommand<TResponse>, TResponse>.Handle))!
            .Invoke(handler, [command, cancellationToken]);

        return (Task<Result<TResponse>>)result!;
    }

    public Task<Result> Send(
        ICommand command,
        CancellationToken cancellationToken)
    {
        Type handlerType = typeof(ICommandHandler<>).MakeGenericType(command.GetType());
        object handler = serviceProvider.GetRequiredService(handlerType);
        object? result = handlerType
            .GetMethod(nameof(ICommandHandler<ICommand>.Handle))!
            .Invoke(handler, [command, cancellationToken]);

        return (Task<Result>)result!;
    }
}
