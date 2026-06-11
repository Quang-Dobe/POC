namespace Poc.Bff.Infrastructure.Tests.Cookies;

using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Time.Testing;
using Poc.Bff.Infrastructure.Cookies;
using Poc.Bff.Infrastructure.Session;
using Xunit;

public class SlidingCookieEventsTests
{
    private static readonly DateTimeOffset Origin = new(2026, 6, 12, 12, 0, 0, TimeSpan.Zero);

    private static CookieValidatePrincipalContext NewContext(ClaimsPrincipal principal)
    {
        var httpContext = new DefaultHttpContext();
        var scheme = new AuthenticationScheme(
            CookieAuthenticationDefaults.AuthenticationScheme,
            displayName: null,
            handlerType: typeof(CookieAuthenticationHandler));
        var options = new CookieAuthenticationOptions();
        var ticket = new AuthenticationTicket(principal, CookieAuthenticationDefaults.AuthenticationScheme);
        return new CookieValidatePrincipalContext(httpContext, scheme, options, ticket);
    }

    [Fact]
    public async Task ValidatePrincipal_sets_ShouldRenew_on_an_authenticated_request()
    {
        var events = new SlidingCookieEvents(new FakeTimeProvider(Origin));
        var identity = new ClaimsIdentity(
            new[] { new Claim(SessionDefaults.SubjectClaimType, "user-123") },
            CookieAuthenticationDefaults.AuthenticationScheme,
            nameType: SessionDefaults.SubjectClaimType,
            roleType: SessionDefaults.RolesClaimType);
        var context = NewContext(new ClaimsPrincipal(identity));

        await events.ValidatePrincipal(context);

        Assert.True(context.ShouldRenew);
    }

    [Fact]
    public async Task ValidatePrincipal_does_not_renew_an_unauthenticated_principal()
    {
        var events = new SlidingCookieEvents(new FakeTimeProvider(Origin));
        var context = NewContext(new ClaimsPrincipal(new ClaimsIdentity()));

        await events.ValidatePrincipal(context);

        Assert.False(context.ShouldRenew);
    }

    [Fact]
    public void Constructor_requires_a_time_provider()
    {
        Assert.Throws<ArgumentNullException>(() => new SlidingCookieEvents(null!));
    }
}
