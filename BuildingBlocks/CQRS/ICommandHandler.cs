using PAN.Mediator.Abstractions;

namespace BuildingBlocks.CQRS
{
    public interface ICommandHandler<in TCommand> : ICommandHandler<TCommand, Unit>
        where TCommand : ICommand<Unit>
    {

    }

    public interface ICommandHandler<in TCommand, TResponse> : IPanRequestHandler<TCommand, TResponse> 
        where TCommand : ICommand<TResponse>
        where TResponse : notnull
    {
    }
}
