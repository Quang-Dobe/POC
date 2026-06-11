using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Poc.Bff.Api.Errors;
using Poc.Bff.Application.Abstractions;
using Poc.Bff.Application.Configuration;
using Poc.Bff.Application.Common.Dispatching;
using Poc.Bff.Application.Features.Auth.Callback;
using Poc.Bff.Application.Features.Auth.Login;
using Poc.Bff.Application.Features.Auth.Logout;
using Poc.Bff.Application.Features.Auth.Me;
using Poc.Bff.Application.Features.Invites.CreateInvite;
using Poc.Bff.Infrastructure.Oidc;
using Poc.Bff.Infrastructure.Rbac;
using BffSessionOptions = Poc.Bff.Infrastructure.Configuration.SessionOptions;
using Poc.Bff.Infrastructure.Session;
using System.Security.Claims;

namespace Poc.Bff.Api.Controllers;

[ApiController]
public sealed class AuthController : ControllerBase
{
    private const string AccessDeniedError = "access_denied";

    private readonly ISender _sender;
    private readonly OidcCorrelationCookie _correlationCookie;
    private readonly TimeProvider _timeProvider;
    private readonly IOptions<BffSessionOptions> _sessionOptions;
    private readonly IOptions<AuthOptions> _authOptions;

    public AuthController(
        ISender sender,
        OidcCorrelationCookie correlationCookie,
        TimeProvider timeProvider,
        IOptions<BffSessionOptions> sessionOptions,
        IOptions<AuthOptions> authOptions)
    {
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentNullException.ThrowIfNull(correlationCookie);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(sessionOptions);
        ArgumentNullException.ThrowIfNull(authOptions);

        _sender = sender;
        _correlationCookie = correlationCookie;
        _timeProvider = timeProvider;
        _sessionOptions = sessionOptions;
        _authOptions = authOptions;
    }

    [AllowAnonymous]
    [HttpGet("/auth/login")]
    public async Task<IActionResult> LoginAsync(CancellationToken ct)
    {
        var result = await _sender.Send(new LoginQuery(), ct);

        if (result.IsFailure)
        {
            return result.Error.ToProblem();
        }

        var redirect = result.Value;

        var correlation = new OidcAuthCorrelation(
            State: redirect.State,
            CodeVerifier: redirect.CodeVerifier,
            Nonce: redirect.Nonce);

        _correlationCookie.Append(Response, correlation, _timeProvider.GetUtcNow());

        return Redirect(redirect.AuthorizeUrl);
    }

    [AllowAnonymous]
    [HttpGet("/auth/callback")]
    public async Task<IActionResult> CallbackAsync(
        [FromQuery] string? code,
        [FromQuery] string? state,
        CancellationToken ct)
    {
        var correlation = _correlationCookie.Read(Request);

        if (correlation is null || string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
        {
            _correlationCookie.Delete(Response);
            return RedirectToFrontendError();
        }

        var command = new CallbackCommand(
            Code: code,
            State: state,
            ExpectedState: correlation.State,
            CodeVerifier: correlation.CodeVerifier,
            Nonce: correlation.Nonce);

        var result = await _sender.Send(command, ct);

        if (result.IsFailure)
        {
            _correlationCookie.Delete(Response);
            return RedirectToFrontendError();
        }

        var session = result.Value;

        var claims = new List<Claim>
        {
            new(SessionDefaults.SubjectClaimType, session.Subject),
            new(SessionDefaults.DisplayNameClaimType, session.DisplayName),
            new(SessionDefaults.RegionClaimType, session.Region),
            new(SessionDefaults.SessionIdClaimType, Guid.NewGuid().ToString("N")),
        };

        foreach (var role in session.Roles)
        {
            claims.Add(new Claim(SessionDefaults.RolesClaimType, role));
        }

        var identity = new ClaimsIdentity(
            claims,
            CookieAuthenticationDefaults.AuthenticationScheme,
            nameType: SessionDefaults.SubjectClaimType,
            roleType: SessionDefaults.RolesClaimType);

        var expiresAt = _timeProvider.GetUtcNow()
            .AddMinutes(_sessionOptions.Value.TtlMinutes);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = expiresAt,
            }).ConfigureAwait(false);

        _correlationCookie.Delete(Response);

        return Redirect(session.RedirectUrl);
    }

    [Authorize(Policy = SessionDefaults.Policy)]
    [HttpGet("/auth/me")]
    public async Task<IActionResult> MeAsync(CancellationToken ct)
    {
        var displayName = User.FindFirst(SessionDefaults.DisplayNameClaimType)?.Value ?? string.Empty;
        var roles = User.FindAll(SessionDefaults.RolesClaimType)
            .Select(c => c.Value)
            .ToArray();

        var result = await _sender.Send(new MeQuery(displayName, roles), ct);

        return result.IsSuccess
            ? Ok(result.Value)
            : result.Error.ToProblem();
    }

    [Authorize(Policy = SessionDefaults.Policy)]
    [HttpPost("/auth/logout")]
    public async Task<IActionResult> LogoutAsync(CancellationToken ct)
    {
        var result = await _sender.Send(new LogoutCommand(), ct);

        if (result.IsFailure)
        {
            return result.Error.ToProblem();
        }

        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme)
            .ConfigureAwait(false);

        return NoContent();
    }

    [Authorize(Policy = RbacPolicies.InvitePolicy)]
    [HttpPost("/auth/invite")]
    public async Task<IActionResult> InviteAsync([FromBody] CreateInviteCommand command, CancellationToken ct)
    {
        var result = await _sender.Send(command, ct);

        return result.IsSuccess
            ? Ok(result.Value)
            : result.Error.ToProblem();
    }

    private IActionResult RedirectToFrontendError()
    {
        var url = QueryHelpers.AddQueryString(
            _authOptions.Value.FrontendReturnUrl,
            "error",
            AccessDeniedError);

        return Redirect(url);
    }
}
