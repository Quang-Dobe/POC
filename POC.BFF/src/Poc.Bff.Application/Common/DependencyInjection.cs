using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Poc.Bff.Application.Common.Behaviors;
using Poc.Bff.Application.Common.Dispatching;

namespace Poc.Bff.Application.Common;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        services.AddTransient<ISender, Sender>();

        foreach (var type in assembly.GetTypes().Where(t => t is { IsAbstract: false, IsInterface: false }))
        {
            foreach (var iface in type.GetInterfaces()
                         .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequestHandler<,>)))
            {
                services.AddTransient(iface, type);
            }
        }

        services.AddValidatorsFromAssembly(assembly, ServiceLifetime.Transient);

        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingPipelineBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationPipelineBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(PerformancePipelineBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ExceptionHandlingPipelineBehavior<,>));

        return services;
    }
}
