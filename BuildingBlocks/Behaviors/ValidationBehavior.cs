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
