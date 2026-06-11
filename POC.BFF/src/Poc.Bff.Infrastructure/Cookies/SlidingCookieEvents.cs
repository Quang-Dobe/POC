namespace Poc.Bff.Infrastructure.Cookies;

using Microsoft.AspNetCore.Authentication.Cookies;

public sealed class SlidingCookieEvents : CookieAuthenticationEvents
{
    private readonly TimeProvider _timeProvider;

    public SlidingCookieEvents(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        _timeProvider = timeProvider;
    }

    public TimeProvider TimeProvider => _timeProvider;

    public override Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Principal?.Identity?.IsAuthenticated == true)
        {
            context.ShouldRenew = true;
        }

        return Task.CompletedTask;
    }
}
