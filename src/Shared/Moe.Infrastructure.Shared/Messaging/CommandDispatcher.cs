using Microsoft.Extensions.DependencyInjection;
using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Persistence;
using Moe.SharedKernel.Results;

namespace Moe.Infrastructure.Shared.Messaging;

internal sealed class CommandDispatcher(IServiceProvider serviceProvider) : ICommandDispatcher
{
    public async Task<Result<TResponse>> Send<TResponse>(
        ICommand<TResponse> command,
        CancellationToken cancellationToken)
    {
        Type handlerType = typeof(ICommandHandler<,>).MakeGenericType(command.GetType(), typeof(TResponse));
        object handler = serviceProvider.GetRequiredService(handlerType);
        object? resultTask = handlerType
            .GetMethod(nameof(ICommandHandler<ICommand<TResponse>, TResponse>.Handle))!
            .Invoke(handler, [command, cancellationToken]);

        var result = await (Task<Result<TResponse>>)resultTask!;

        if (result.IsSuccess)
        {
            var unitOfWork = serviceProvider.GetService<IUnitOfWork>();
            if (unitOfWork is not null)
            {
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }

        return result;
    }

    public async Task<Result> Send(
        ICommand command,
        CancellationToken cancellationToken)
    {
        Type handlerType = typeof(ICommandHandler<>).MakeGenericType(command.GetType());
        object handler = serviceProvider.GetRequiredService(handlerType);
        object? resultTask = handlerType
            .GetMethod(nameof(ICommandHandler<ICommand>.Handle))!
            .Invoke(handler, [command, cancellationToken]);

        var result = await (Task<Result>)resultTask!;

        if (result.IsSuccess)
        {
            var unitOfWork = serviceProvider.GetService<IUnitOfWork>();
            if (unitOfWork is not null)
            {
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }

        return result;
    }
}
