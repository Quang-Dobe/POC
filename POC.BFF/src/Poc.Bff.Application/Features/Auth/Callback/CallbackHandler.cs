using Microsoft.Extensions.Options;
using Poc.Bff.Application.Abstractions;
using Poc.Bff.Application.Common.Dispatching;
using Poc.Bff.Application.Common.Results;
using Poc.Bff.Application.Configuration;

namespace Poc.Bff.Application.Features.Auth.Callback;

public sealed class CallbackHandler : IRequestHandler<CallbackCommand, Result<SessionEstablished>>
{
    private readonly IOidcAuthClient _oidcAuthClient;
    private readonly IRoleResolver _roleResolver;
    private readonly IOptions<AuthOptions> _authOptions;

    public CallbackHandler(
        IOidcAuthClient oidcAuthClient,
        IRoleResolver roleResolver,
        IOptions<AuthOptions> authOptions)
    {
        ArgumentNullException.ThrowIfNull(oidcAuthClient);
        ArgumentNullException.ThrowIfNull(roleResolver);
        ArgumentNullException.ThrowIfNull(authOptions);

        _oidcAuthClient = oidcAuthClient;
        _roleResolver = roleResolver;
        _authOptions = authOptions;
    }

    public async Task<Result<SessionEstablished>> Handle(
        CallbackCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!string.Equals(request.State, request.ExpectedState, StringComparison.Ordinal))
        {
            return Result.Failure<SessionEstablished>(AuthErrors.StateMismatch);
        }

        var correlation = new OidcAuthCorrelation(
            State: request.ExpectedState,
            CodeVerifier: request.CodeVerifier,
            Nonce: request.Nonce);

        ExternalIdentity identity;
        try
        {
            identity = await _oidcAuthClient
                .ExchangeCodeAsync(request.Code, correlation, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OidcAuthException)
        {
            return Result.Failure<SessionEstablished>(AuthErrors.CodeExchangeFailed);
        }

        var resolution = _roleResolver.Resolve(identity.Subject);
        if (!resolution.IsAllowed)
        {
            return Result.Failure<SessionEstablished>(AuthErrors.AccessDenied);
        }

        var sessionEstablished = new SessionEstablished(
            Subject: identity.Subject,
            DisplayName: identity.DisplayName,
            Region: resolution.Region,
            Roles: resolution.Roles,
            RedirectUrl: _authOptions.Value.FrontendReturnUrl);

        return Result.Success(sessionEstablished);
    }
}
