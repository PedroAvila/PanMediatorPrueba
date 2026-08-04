using BuildingBlocks.Behaviors;
using BuildingBlocks.Exceptions.Handler;
using DotNetEnv;
using FluentValidation;
using PAN.Mediator.DependencyInjection;
using PanMediatorPrueba.Repository;

var builder = WebApplication.CreateBuilder(args);
Env.Load();

builder.Services.AddControllers();

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<CustomExceptionHandler>();
builder.Services.AddValidatorsFromAssembly(typeof(Program).Assembly);

builder.Services.AddPanMediator(config =>
{
    config.RegisterServicesFromAssembly(typeof(Program).Assembly);
    config.AddOpenBehavior(typeof(LoggingBehavior<,>));
    config.AddOpenBehavior(typeof(ValidationBehavior<,>));
});

var connectionString =
    Environment.GetEnvironmentVariable("DB_CONNECTION_STRING")
    ?? throw new InvalidOperationException("DB_CONNECTION_STRING no está configurada");

builder.Services.AddScoped<ICustomerRepository>(provider => new CustomerRepository(
    connectionString
));

var app = builder.Build();

app.UseExceptionHandler(opt => { });

app.UseAuthorization();
app.MapControllers();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseExceptionHandler();
app.UseHttpsRedirection();

app.Run();
