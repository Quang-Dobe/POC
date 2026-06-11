using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;

namespace Poc.Bff.Integration.Tests.Support;

public static class CookieTestSupport
{
    public static void UseEphemeralDataProtection(IServiceCollection services) =>
        services.AddDataProtection().UseEphemeralDataProtectionProvider();
}
