using BuildingBlocks.CQRS;
using FluentValidation;
using PanMediatorPrueba.Entities;
using PanMediatorPrueba.Repository;

namespace PanMediatorPrueba.Features.Customers;

public record CreateCustomerCommand(string Name, string Address, string Phone)
    : ICommand<CreateCustomerResult>;

public record CreateCustomerResult(int Id, string Name, string Address, string Phone);

public class CreateCustomerCommandValidator : AbstractValidator<CreateCustomerCommand>
{
    public CreateCustomerCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Name cannot be empty");
        RuleFor(x => x.Address).NotEmpty().WithMessage("Address cannot be empty");
        RuleFor(x => x.Phone).NotEmpty().WithMessage("Phone cannot be empty");
    }
}

public class CreateCustomerCommandHandler(ICustomerRepository customerRepository)
    : ICommandHandler<CreateCustomerCommand, CreateCustomerResult>
{
    public async ValueTask<CreateCustomerResult> Handle(
        CreateCustomerCommand command,
        CancellationToken cancellationToken
    )
    {
        var customer = new Customer
        {
            Name = command.Name,
            Address = command.Address,
            Phone = command.Phone,
        };

        var id = await customerRepository.CreateAsync(customer);
        return new CreateCustomerResult(
            Id: id,
            Name: customer.Name,
            Address: customer.Address,
            Phone: customer.Phone
        );
    }
}
