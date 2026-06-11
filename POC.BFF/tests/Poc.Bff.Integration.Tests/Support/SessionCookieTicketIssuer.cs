using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Poc.Bff.Infrastructure.Session;
using System.Security.Claims;

namespace Poc.Bff.Integration.Tests.Support;

public static class SessionCookieTicketIssuer
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
            new(SessionDefaults.SubjectClaimType, subject),
            new(SessionDefaults.DisplayNameClaimType, displayName),
            new(SessionDefaults.RegionClaimType, region),
            new(SessionDefaults.SessionIdClaimType, Guid.NewGuid().ToString("N")),
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim(SessionDefaults.RolesClaimType, role));
        }

        var identity = new ClaimsIdentity(
            claims,
            CookieAuthenticationDefaults.AuthenticationScheme,
            nameType: SessionDefaults.SubjectClaimType,
            roleType: SessionDefaults.RolesClaimType);

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
