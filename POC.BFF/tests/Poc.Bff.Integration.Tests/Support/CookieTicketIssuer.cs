using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace Poc.Bff.Integration.Tests.Support;

public static class CookieTicketIssuer
{
    public static string IssueCookiePair(
        IServiceProvider services,
        string cookieName,
        string subject,
        string displayName,
        string region,
        params string[] roles)
    {
        var options = services
            .GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(CookieAuthenticationDefaults.AuthenticationScheme);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, subject),
            new(ClaimTypes.Name, displayName),
            new("region", region),
            new("sid", Guid.NewGuid().ToString("N")),
        };
        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var identity = new ClaimsIdentity(
            claims,
            CookieAuthenticationDefaults.AuthenticationScheme,
            nameType: ClaimTypes.NameIdentifier,
            roleType: ClaimTypes.Role);

        var properties = new AuthenticationProperties
        {
            IsPersistent = true,
            ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(30),
        };

        var ticket = new AuthenticationTicket(
            new ClaimsPrincipal(identity),
            properties,
            CookieAuthenticationDefaults.AuthenticationScheme);

        var protectedValue = options.TicketDataFormat.Protect(ticket);
        return $"{cookieName}={protectedValue}";
    }
}
