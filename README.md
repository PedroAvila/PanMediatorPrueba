# PanMediatorPrueba

Proyecto de prueba del uso de **PAN.Mediator** (v1.0.0), un mediador inmutable, ligero y nativo para CQRS y Arquitectura Hexagonal en .NET, diseñado como reemplazo de MediatR con paridad de superficie de API (`Send`/`Publish`, pipeline behaviors, `Unit`).

Este proyecto muestra cómo integrar PAN.Mediator en una API ASP.NET Core 8 junto con FluentValidation (validación), Dapper (acceso a datos) y SQL Server.

## Requisitos

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- SQL Server local (por defecto en `localhost,1433`)
- Paquetes NuGet principales:
  - `PAN.Mediator` 1.0.0
  - `FluentValidation` / `FluentValidation.AspNetCore`
  - `Mapster`
  - `Dapper`
  - `Microsoft.Data.SqlClient`
  - `DotNetEnv`

## Configuración inicial

1. Clona o abre el proyecto.
2. Configura la cadena de conexión en el archivo `.env` (raíz del proyecto):

   ```env
   DB_CONNECTION_STRING=Server=localhost,1433;Database=Prueba;User Id=sa;Password=TuPassword;TrustServerCertificate=True;
   ```

3. Crea la base de datos y la tabla `Customers`:

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

4. Ejecuta la aplicación:

   ```bash
   dotnet restore
   dotnet run
   ```

5. Abre Swagger en `https://localhost:<puerto>/swagger`.

## Estructura del proyecto

| Carpeta | Descripción |
| --- | --- |
| `BuildingBlocks/` | Proyecto bloque de construcción: abstracciones CQRS, pipeline behaviors, excepciones y utilidades. |
| `BuildingBlocks/CQRS/` | Interfaces `ICommand`, `ICommandHandler`, `IQuery`, `IQueryHandler` (envuelven las abstracciones de PAN.Mediator). |
| `BuildingBlocks/Behaviors/` | Pipeline behaviors: `LoggingBehavior` y `ValidationBehavior`. |
| `BuildingBlocks/Exceptions/` | `BusinessException` y `CustomExceptionHandler` (RFC 7807 ProblemDetails). |
| `Features/` | Handlers organizados por feature (`Customers`, `Orders`). |
| `Controllers/` | Endpoints HTTP que envían commands/queries al mediador. |
| `Repository/` | Acceso a datos con Dapper (`ICustomerRepository`, `CustomerRepository`). |
| `Entities/` | Modelos de dominio (`Customer`). |

## Uso de PAN.Mediator

### 1. Registro del mediador

En `Program.cs` se registra el mediador indicando el assembly a escanear y los pipeline behaviors:

```csharp
builder.Services.AddPanMediator(config =>
{
    config.RegisterServicesFromAssembly(typeof(Program).Assembly);
    config.AddOpenBehavior(typeof(LoggingBehavior<,>));
    config.AddOpenBehavior(typeof(ValidationBehavior<,>));
});
```

`RegisterServicesFromAssembly` registra automáticamente todos los handlers `IPanRequestHandler<,>` e `IPanNotificationHandler<>` del assembly.

### 2. Abstracciones CQRS propias

`BuildingBlocks` define sus propias interfaces que heredan de las de PAN.Mediator:

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

### 3. Command con validación (escribir)

`Features/Customers/CreateCustomer.cs` define el command, su resultado, el validador de FluentValidation y el handler:

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

### 4. Query (leer)

`Features/Orders/GetOrder.cs` define una query con su respuesta y handler:

```csharp
public record OrderResponse(Guid Id, string CustomerName, decimal Amount);

public record GetOrderQuery(Guid OrderId) : IQuery<OrderResponse>;

public class GetOrderQueryHandler : IQueryHandler<GetOrderQuery, OrderResponse>
{
    public Task<OrderResponse> Handle(GetOrderQuery query, CancellationToken cancellationToken)
        => Task.FromResult(new OrderResponse(query.OrderId, "Cliente de prueba", 99.99m));
}
```

### 5. Enviar desde el controlador

El controller inyecta `IPanMediator` y usa `Send`:

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

- **`LoggingBehavior<TRequest, TResponse>`** — Registra el inicio/fin de cada request y advierte (warning) si tarda más de 3 segundos.
- **`ValidationBehavior<TRequest, TResponse>`** — Ejecuta los validadores de FluentValidation registrados y lanza `ValidationException` si hay errores.

El orden de registro importa: el primer behavior registrado queda más "afuera" en la cadena. Aquí `LoggingBehavior` envuelve a `ValidationBehavior`, que envuelve al handler.

### 7. Manejo de excepciones

`CustomExceptionHandler` traduce las excepciones a `ProblemDetails` (RFC 7807):

- `ValidationException` → `400 Bad Request` (con detalle de errores).
- `BusinessException` → el código de estado que indique la excepción.
- Cualquier otra → `500 Internal Server Error`.

```csharp
builder.Services.AddExceptionHandler<CustomExceptionHandler>();
builder.Services.AddProblemDetails();
```

## Endpoints de ejemplo

### POST /api/Customer

Crea un cliente y devuelve los datos creados:

```http
POST /api/Customer
Content-Type: application/json

{
  "name": "Juan Pérez",
  "address": "Av. Siempre Viva 123",
  "phone": "555-1234"
}
```

Respuesta (HTTP 201):

```json
{
  "id": 1,
  "name": "Juan Pérez",
  "address": "Av. Siempre Viva 123",
  "phone": "555-1234"
}
```

Si la validación falla, responde HTTP 400 con `ProblemDetails` y la lista de errores en `ValidationErrors`.
