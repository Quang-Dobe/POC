using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Poc.Bff.Application.Common.Dispatching;

namespace Poc.Bff.Application.Common.Behaviors;

public sealed class PerformancePipelineBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private const long SlowThresholdMs = 500;
    private readonly ILogger<PerformancePipelineBehavior<TRequest, TResponse>> _logger;
    public PerformancePipelineBehavior(ILogger<PerformancePipelineBehavior<TRequest, TResponse>> logger) => _logger = logger;

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        var response = await next();
        sw.Stop();
        if (sw.ElapsedMilliseconds > SlowThresholdMs)
            _logger.LogWarning("{RequestName} took {Elapsed}ms", typeof(TRequest).Name, sw.ElapsedMilliseconds);
        return response;
    }
}
