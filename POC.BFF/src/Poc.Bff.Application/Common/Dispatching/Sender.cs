using Microsoft.Extensions.DependencyInjection;

namespace Poc.Bff.Application.Common.Dispatching;

public sealed class Sender : ISender
{
    private readonly IServiceProvider _provider;

    public Sender(IServiceProvider provider) => _provider = provider;

    public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        var requestType = request.GetType();
        var handlerType = typeof(IRequestHandler<,>).MakeGenericType(requestType, typeof(TResponse));
        var behaviorType = typeof(IPipelineBehavior<,>).MakeGenericType(requestType, typeof(TResponse));

        var handler = _provider.GetRequiredService(handlerType);
        var handlerMethod = handlerType.GetMethod("Handle")!;

        RequestHandlerDelegate<TResponse> pipeline = () =>
            (Task<TResponse>)handlerMethod.Invoke(handler, new object[] { request, cancellationToken })!;

        var behaviors = ((IEnumerable<object>)_provider.GetServices(behaviorType)).ToList();
        var behaviorMethod = behaviorType.GetMethod("Handle")!;

        for (var i = behaviors.Count - 1; i >= 0; i--)
        {
            var behavior = behaviors[i];
            var next = pipeline;
            pipeline = () =>
                (Task<TResponse>)behaviorMethod.Invoke(behavior, new object[] { request, next, cancellationToken })!;
        }

        return pipeline();
    }
}
