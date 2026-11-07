# PanMediatorPrueba

Sample project demonstrating the use of **PAN.Mediator** (v2.0.0), an immutable, lightweight, and native mediator for CQRS and Hexagonal Architecture in .NET, designed as a MediatR replacement with API surface parity (`Send`/`Publish`, pipeline behaviors, `Unit`).

This project shows how to integrate PAN.Mediator into an ASP.NET Core 8 API together with FluentValidation (validation), Dapper (data access), and SQL Server.

## Requirements

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Local SQL Server (default at `localhost,1433`)
- Main NuGet packages:
  - `PAN.Mediator` 2.0.0
  - `FluentValidation` / `FluentValidation.AspNetCore`
  - `Mapster`
  - `Dapper`
  - `Microsoft.Data.SqlClient`
  - `DotNetEnv`

## Initial setup

1. Clone or open the project.
2. Configure the connection string in the `.env` file (project root):

   ```env
   DB_CONNECTION_STRING=Server=localhost,1433;Database=Prueba;User Id=sa;Password=TuPassword;TrustServerCertificate=True;
   ```

3. Create the database and the `Customers` table:

   ```sql
   CREATE DATABASE Prueba;
   GO

   USE Prueba;
   GO

   CREATE TABLE dbo.Customers
   (
       Id      INT IDENTITY(1,1) PRIMARY KEY,
       Name    NVARCHAR(200) NOT NULL,
       Address NVARCHAR(200) NOT NULL,
       Phone   NVARCHAR(50)  NOT NULL
   );
   ```

4. Run the application:

   ```bash
   dotnet restore
   dotnet run
   ```

5. Open Swagger at `https://localhost:<port>/swagger`.

## Project structure

| Folder | Description |
| --- | --- |
| `BuildingBlocks/` | Building block project: CQRS abstractions, pipeline behaviors, exceptions, and utilities. |
| `BuildingBlocks/CQRS/` | `ICommand`, `ICommandHandler`, `IQuery`, `IQueryHandler` interfaces (wrapping PAN.Mediator abstractions). |
| `BuildingBlocks/Behaviors/` | Pipeline behaviors: `LoggingBehavior` and `ValidationBehavior`. |
| `BuildingBlocks/Exceptions/` | `BusinessException` and `CustomExceptionHandler` (RFC 7807 ProblemDetails). |
| `Features/` | Handlers organized by feature (`Customers`, `Orders`). |
| `Controllers/` | HTTP endpoints that send commands/queries to the mediator. |
| `Repository/` | Data access with Dapper (`ICustomerRepository`, `CustomerRepository`). |
| `Entities/` | Domain models (`Customer`). |

## Using PAN.Mediator

### 1. Registering the mediator

In `Program.cs`, the mediator is registered by specifying the assembly to scan and the pipeline behaviors:

```csharp
builder.Services.AddPanMediator(config =>
{
    config.RegisterServicesFromAssembly(typeof(Program).Assembly);
    config.AddOpenBehavior(typeof(LoggingBehavior<,>));
    config.AddOpenBehavior(typeof(ValidationBehavior<,>));
});
```

`RegisterServicesFromAssembly` automatically registers all `IPanRequestHandler<,>` and `IPanNotificationHandler<>` handlers in the assembly.

### 2. Custom CQRS abstractions

`BuildingBlocks` defines its own interfaces that inherit from PAN.Mediator's:

```csharp
// ICommand.cs
public interface ICommand : ICommand<Unit> { }
public interface ICommand<out TResponse> : IPanRequest<TResponse> { }

// IQuery.cs
public interface IQuery<out TResponse> : IPanRequest<TResponse> where TResponse : notnull { }
```

```csharp
// ICommandHandler.cs
public interface ICommandHandler<in TCommand, TResponse> : IPanRequestHandler<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
    where TResponse : notnull { }

// IQueryHandler.cs
public interface IQueryHandler<in TQuery, TResponse> : IPanRequestHandler<TQuery, TResponse>
    where TQuery : IQuery<TResponse>
    where TResponse : notnull { }
```

### 3. Command with validation (write)

`Features/Customers/CreateCustomer.cs` defines the command, its result, the FluentValidation validator, and the handler:

```csharp
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
    public async Task<CreateCustomerResult> Handle(
        CreateCustomerCommand command,
        CancellationToken cancellationToken)
    {
        var customer = new Customer
        {
            Name = command.Name,
            Address = command.Address,
            Phone = command.Phone,
        };

        var id = await customerRepository.CreateAsync(customer);
        return new CreateCustomerResult(id, customer.Name!, customer.Address!, customer.Phone!);
    }
}
```

### 4. Query (read)

`Features/Orders/GetOrder.cs` defines a query with its response and handler:

```csharp
public record OrderResponse(Guid Id, string CustomerName, decimal Amount);

public record GetOrderQuery(Guid OrderId) : IQuery<OrderResponse>;

public class GetOrderQueryHandler : IQueryHandler<GetOrderQuery, OrderResponse>
{
    public Task<OrderResponse> Handle(GetOrderQuery query, CancellationToken cancellationToken)
        => Task.FromResult(new OrderResponse(query.OrderId, "Cliente de prueba", 99.99m));
}
```

### 5. Sending from the controller

The controller injects `IPanMediator` and uses `Send`:

```csharp
[ApiController]
[Route("api/[controller]")]
public class CustomerController : ControllerBase
{
    private readonly IPanMediator _mediator;

    public CustomerController(IPanMediator mediator) => _mediator = mediator;

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCustomerCommand dto)
    {
        var result = await _mediator.Send(dto);
        return Created(string.Empty, result);
    }
}
```

### 6. Pipeline behaviors

- **`LoggingBehavior<TRequest, TResponse>`** — Logs the start/end of each request and warns if it takes longer than 3 seconds.
- **`ValidationBehavior<TRequest, TResponse>`** — Runs the registered FluentValidation validators and throws `ValidationException` if there are errors.

Registration order matters: the first registered behavior ends up on the "outside" of the chain. Here `LoggingBehavior` wraps `ValidationBehavior`, which wraps the handler.

### 7. Exception handling

`CustomExceptionHandler` translates exceptions into `ProblemDetails` (RFC 7807):

- `ValidationException` → `400 Bad Request` (with error details).
- `BusinessException` → the status code specified by the exception.
- Anything else → `500 Internal Server Error`.

```csharp
builder.Services.AddExceptionHandler<CustomExceptionHandler>();
builder.Services.AddProblemDetails();
```

## Sample endpoints

### POST /api/Customer

Creates a customer and returns the created data:

```http
POST /api/Customer
Content-Type: application/json

{
  "name": "Juan Pérez",
  "address": "Av. Siempre Viva 123",
  "phone": "555-1234"
}
```

Response (HTTP 201):

```json
{
  "id": 1,
  "name": "Juan Pérez",
  "address": "Av. Siempre Viva 123",
  "phone": "555-1234"
}
```

If validation fails, it responds with HTTP 400, `ProblemDetails`, and the list of errors in `ValidationErrors`.

---

## PAN.Mediator v2 implementation (`ValueTask`)

The earlier sections were written for the original v1.0.0 sample and are kept for reference. The project now runs on PAN.Mediator **2.0.0**, which changed the contract of handlers and pipeline behaviors from `Task`/`Task<T>` to `ValueTask`/`ValueTask<T>`. The code below shows the current implementation.

### Files changed for v2

| File | Change |
| --- | --- |
| `BuildingBlocks/BuildingBlocks.csproj` | `PAN.Mediator` bumped from `1.0.0` to `2.0.0`. |
| `BuildingBlocks/Behaviors/LoggingBehavior.cs` | `Handle` now returns `ValueTask<TResponse>`. |
| `BuildingBlocks/Behaviors/ValidationBehavior.cs` | `Handle` now returns `ValueTask<TResponse>`; local typo `validationResuls` fixed to `validationResults`. |
| `Features/Customers/CreateCustomer.cs` | `CreateCustomerCommandHandler.Handle` now returns `ValueTask<CreateCustomerResult>`. |
| `Features/Orders/CreateOrder.cs` | `Handle` now returns `ValueTask<Guid>` and uses `ValueTask.FromResult`. |
| `Features/Orders/GetOrder.cs` | `Handle` now returns `ValueTask<OrderResponse>` and uses `ValueTask.FromResult`. |

### Package

`BuildingBlocks/BuildingBlocks.csproj`:

```xml
<PackageReference Include="PAN.Mediator" Version="2.0.0" />
```

The 2.0.0 NuGet package also ships a Roslyn source generator that emits a compile-time dispatch chain for the concrete requests, falling back to reflection only for unknown request types.

### Key change: `ValueTask`

In v2, `PanRequestHandlerDelegate<TResponse>` returns `ValueTask<TResponse>`, `IPanPipelineBehavior<,>.Handle` returns `ValueTask<TResponse>`, and `IPanRequestHandler<,>.Handle` returns `ValueTask<TResponse>`:

```csharp
public delegate ValueTask<TResponse> PanRequestHandlerDelegate<TResponse>();

public interface IPanPipelineBehavior<in TRequest, TResponse>
{
    ValueTask<TResponse> Handle(
        TRequest request,
        PanRequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken);
}

public interface IPanRequestHandler<in TRequest, TResponse>
{
    ValueTask<TResponse> Handle(TRequest request, CancellationToken cancellationToken);
}
```

### `ValidationBehavior` (v2)

`BuildingBlocks/Behaviors/ValidationBehavior.cs`:

```csharp
using BuildingBlocks.CQRS;
using FluentValidation;
using PAN.Mediator.Abstractions;

namespace BuildingBlocks.Behaviors
{
    public class ValidationBehavior<TRequest, TResponse>(
        IEnumerable<IValidator<TRequest>> validators
    ) : IPanPipelineBehavior<TRequest, TResponse>
        where TRequest : ICommand<TResponse>
    {
        public async ValueTask<TResponse> Handle(
            TRequest request,
            PanRequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken
        )
        {
            var context = new ValidationContext<TRequest>(request);

            var validationResults = await Task.WhenAll(
                validators.Select(v => v.ValidateAsync(context, cancellationToken))
            );

            var failures = validationResults
                .Where(r => r.Errors.Any())
                .SelectMany(r => r.Errors)
                .ToList();

            if (failures.Any())
                throw new ValidationException(failures);

            return await next();
        }
    }
}
```

Because the generic constraint is `where TRequest : ICommand<TResponse>`, this behavior applies only to commands. PAN.Mediator skips it for queries (`IQuery<TResponse>`) without throwing. If queries should also be validated, change the constraint to `where TRequest : IPanRequest<TResponse>`.

### `LoggingBehavior` (v2)

`BuildingBlocks/Behaviors/LoggingBehavior.cs`:

```csharp
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using PAN.Mediator.Abstractions;

namespace BuildingBlocks.Behaviors
{
    public class LoggingBehavior<TRequest, TResponse>(
        ILogger<LoggingBehavior<TRequest, TResponse>> logger
    ) : IPanPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull, IPanRequest<TResponse>
        where TResponse : notnull
    {
        public async ValueTask<TResponse> Handle(
            TRequest request,
            PanRequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken
        )
        {
            logger.LogInformation(
                "[START] Handle request={Request} - Response={Response} - RequestData={RequestData}",
                typeof(TRequest).Name,
                typeof(TResponse).Name,
                request
            );

            var timer = new Stopwatch();
            timer.Start();

            var response = await next();

            timer.Stop();
            var timeTaken = timer.Elapsed;
            if (timeTaken.TotalSeconds > 3)
                logger.LogWarning(
                    "[PERFORMANCE] The request {Request} took {TimeTaken} seconds.",
                    typeof(TRequest).Name,
                    timeTaken.Seconds
                );

            logger.LogInformation(
                "[END] Handled {Request} with {Response}",
                typeof(TRequest).Name,
                typeof(TResponse).Name
            );

            return response;
        }
    }
}
```

### Handlers (v2)

Handlers must also return `ValueTask`/`ValueTask<T>`:

```csharp
public class CreateCustomerCommandHandler(ICustomerRepository customerRepository)
    : ICommandHandler<CreateCustomerCommand, CreateCustomerResult>
{
    public async ValueTask<CreateCustomerResult> Handle(
        CreateCustomerCommand command,
        CancellationToken cancellationToken)
    {
        var customer = new Customer
        {
            Name = command.Name,
            Address = command.Address,
            Phone = command.Phone,
        };

        var id = await customerRepository.CreateAsync(customer);
        return new CreateCustomerResult(id, customer.Name, customer.Address, customer.Phone);
    }
}
```

```csharp
public class GetOrderQueryHandler : IQueryHandler<GetOrderQuery, OrderResponse>
{
    public ValueTask<OrderResponse> Handle(
        GetOrderQuery query,
        CancellationToken cancellationToken)
        => ValueTask.FromResult(
            new OrderResponse(query.OrderId, "Cliente de prueba", 99.99m));
}
```

```csharp
public class CreateOrderCommandHandler : ICommandHandler<CreateOrderCommand, Guid>
{
    public ValueTask<Guid> Handle(CreateOrderCommand command, CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(Guid.NewGuid());
    }
}
```

### Registration (v2)

Registration is unchanged. The first registered behavior is the outermost one, so `LoggingBehavior` wraps `ValidationBehavior`, which wraps the handler:

```csharp
builder.Services.AddValidatorsFromAssembly(typeof(Program).Assembly);

builder.Services.AddPanMediator(config =>
{
    config.RegisterServicesFromAssembly(typeof(Program).Assembly);
    config.AddOpenBehavior(typeof(LoggingBehavior<,>));
    config.AddOpenBehavior(typeof(ValidationBehavior<,>));
});
```

### Migration checklist (v1 → v2)

1. Update the `PAN.Mediator` package reference to `2.0.0`.
2. Change every handler signature from `Task<T>` to `ValueTask<T>`.
3. Change every pipeline behavior signature from `Task<TResponse>` to `ValueTask<TResponse>`.
4. Replace `Task.FromResult(...)` with `ValueTask.FromResult(...)` where applicable.
5. Keep the registration calls (`RegisterServicesFromAssembly`, `AddOpenBehavior`) exactly as they are.

`dotnet build` passes with 0 errors and 0 warnings after these changes.
