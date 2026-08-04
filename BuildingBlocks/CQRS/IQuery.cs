using PAN.Mediator.Abstractions;

namespace BuildingBlocks.CQRS
{
    public interface IQuery<out TResponse> : IPanRequest<TResponse> where TResponse : notnull
    {
    }
}
