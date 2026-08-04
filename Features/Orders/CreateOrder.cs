using BuildingBlocks.CQRS;
using FluentValidation;

namespace PanMediatorPrueba.Features.Orders;

public record CreateOrderCommand(string CustomerName, decimal Amount) : ICommand<Guid>;

public class CreateOrderCommandValidator : AbstractValidator<CreateOrderCommand>
{
    public CreateOrderCommandValidator()
    {
        RuleFor(x => x.CustomerName).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Amount).GreaterThan(0);
    }
}

public class CreateOrderCommandHandler : ICommandHandler<CreateOrderCommand, Guid>
{
    public Task<Guid> Handle(CreateOrderCommand command, CancellationToken cancellationToken)
    {
        return Task.FromResult(Guid.NewGuid());
    }
}
