using BuildingBlocks.CQRS;

namespace PanMediatorPrueba.Features.Orders;

public record OrderResponse(Guid Id, string CustomerName, decimal Amount);

public record GetOrderQuery(Guid OrderId) : IQuery<OrderResponse>;

public class GetOrderQueryHandler : IQueryHandler<GetOrderQuery, OrderResponse>
{
    public Task<OrderResponse> Handle(GetOrderQuery query, CancellationToken cancellationToken)
    {
        return Task.FromResult(new OrderResponse(query.OrderId, "Cliente de prueba", 99.99m));
    }
}
