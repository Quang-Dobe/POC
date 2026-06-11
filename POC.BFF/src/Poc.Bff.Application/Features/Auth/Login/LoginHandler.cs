using Poc.Bff.Application.Abstractions;
using Poc.Bff.Application.Common;
using Poc.Bff.Application.Common.Dispatching;
using Poc.Bff.Application.Common.Results;

namespace Poc.Bff.Application.Features.Auth.Login;

public sealed class LoginHandler : IRequestHandler<LoginQuery, Result<LoginRedirect>>
{
    private readonly IOidcAuthClient _oidcAuthClient;

    public LoginHandler(IOidcAuthClient oidcAuthClient)
    {
        ArgumentNullException.ThrowIfNull(oidcAuthClient);
        _oidcAuthClient = oidcAuthClient;
    }

    public async Task<Result<LoginRedirect>> Handle(LoginQuery request, CancellationToken cancellationToken)
    {
        var correlation = new OidcAuthCorrelation(
            State: Pkce.NewOpaqueValue(),
            CodeVerifier: Pkce.NewCodeVerifier(),
            Nonce: Pkce.NewOpaqueValue());

        var authorizeUrl = await _oidcAuthClient
            .BuildAuthorizeUrlAsync(correlation, cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(new LoginRedirect(
            AuthorizeUrl: authorizeUrl,
            State: correlation.State,
            CodeVerifier: correlation.CodeVerifier,
            Nonce: correlation.Nonce));
    }
}
