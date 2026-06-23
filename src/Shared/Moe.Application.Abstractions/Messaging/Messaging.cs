using Moe.SharedKernel.Results;

namespace Moe.Application.Abstractions.Messaging;

public interface ICommand;
public interface ICommand<TResponse>;
public interface IQuery<TResponse>;
public interface ICommandHandler<in TCommand> where TCommand : ICommand
{
    Task<Result> Handle(TCommand command, CancellationToken cancellationToken);
}
public interface ICommandHandler<in TCommand, TResponse> where TCommand : ICommand<TResponse>
{
    Task<Result<TResponse>> Handle(TCommand command, CancellationToken cancellationToken);
}
public interface IQueryHandler<in TQuery, TResponse> where TQuery : IQuery<TResponse>
{
    Task<Result<TResponse>> Handle(TQuery query, CancellationToken cancellationToken);
}

public interface ICommandDispatcher
{
    Task<Result<TResponse>> Send<TResponse>(
        ICommand<TResponse> command,
        CancellationToken cancellationToken);

    Task<Result> Send(
        ICommand command,
        CancellationToken cancellationToken);
}

public interface IQueryDispatcher
{
    Task<Result<TResponse>> Send<TResponse>(
        IQuery<TResponse> query,
        CancellationToken cancellationToken);
}
