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
        public async Task<TResponse> Handle(
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
            if (timeTaken.TotalSeconds > 3) // If the request is greater than 3 seconds, then 3 seconds, then log the warning
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
