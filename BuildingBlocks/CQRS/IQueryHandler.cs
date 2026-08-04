using PAN.Mediator.Abstractions;

namespace BuildingBlocks.CQRS
{
    public interface IQueryHandler<in TQuery, TResponse> : IPanRequestHandler<TQuery, TResponse>
        where TQuery : IQuery<TResponse>
        where TResponse : notnull
    {

    }
}
