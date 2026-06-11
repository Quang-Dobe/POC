using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Poc.Bff.Application.Common.Dispatching;
using Poc.Bff.Application.Common.Results;
using Xunit;

namespace Poc.Bff.Application.Tests.Common;

public class SenderTests
{
    public sealed record Ping(string Text) : IRequest<Result<string>>;

    public sealed class PingHandler : IRequestHandler<Ping, Result<string>>
    {
        public Task<Result<string>> Handle(Ping request, CancellationToken ct)
            => Task.FromResult(Result.Success(request.Text + ":handled"));
    }

    public sealed class TagBehavior : IPipelineBehavior<Ping, Result<string>>
    {
        public async Task<Result<string>> Handle(Ping request, RequestHandlerDelegate<Result<string>> next, CancellationToken ct)
        {
            var inner = await next();
            return Result.Success(inner.Value + ":tagged");
        }
    }

    [Fact]
    public async Task Send_RunsHandler_WrappedByBehavior()
    {
        var services = new ServiceCollection();
        services.AddTransient<ISender, Sender>();
        services.AddTransient<IRequestHandler<Ping, Result<string>>, PingHandler>();
        services.AddTransient<IPipelineBehavior<Ping, Result<string>>, TagBehavior>();
        var provider = services.BuildServiceProvider();

        var result = await provider.GetRequiredService<ISender>().Send(new Ping("hi"));

        result.Value.Should().Be("hi:handled:tagged");
    }
}
