using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StackExchange.Redis;

namespace Poc.Bff.Integration.Tests.Support;

public static class TestRedis
{
    public static void SubstituteMultiplexer(IServiceCollection services)
    {

        var descriptor = services.SingleOrDefault(
            d => d.ServiceType == typeof(IConnectionMultiplexer));

        if (descriptor is not null)
        {
            services.Remove(descriptor);
        }

        services.AddSingleton(Substitute.For<IConnectionMultiplexer>());
    }
}
