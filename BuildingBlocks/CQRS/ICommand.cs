using PAN.Mediator.Abstractions;

namespace BuildingBlocks.CQRS
{
    /// <summary>
    /// BuildingBlocks es un proyecto Bloque de Construcción donde se crea la abstracción del Mediador
    /// </summary>

    public interface ICommand : ICommand<Unit> { }

    public interface ICommand<out TResponse> : IPanRequest<TResponse> { }
}
