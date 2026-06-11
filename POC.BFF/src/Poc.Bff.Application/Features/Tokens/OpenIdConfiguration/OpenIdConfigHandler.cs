using Microsoft.Extensions.Options;
using Poc.Bff.Application.Common.Dispatching;
using Poc.Bff.Application.Common.Results;
using Poc.Bff.Application.Configuration;

namespace Poc.Bff.Application.Features.Tokens.OpenIdConfiguration;

public sealed class OpenIdConfigHandler : IRequestHandler<OpenIdConfigQuery, Result<OpenIdConfigResponse>>
{
    private const string JwksRoute = "/.well-known/jwks.json";

    private static readonly string[] ResponseTypesSupported = { "id_token" };
    private static readonly string[] SubjectTypesSupported = { "public" };
    private static readonly string[] IdTokenSigningAlgValuesSupported = { "RS256" };

    private readonly IdpSimulatorOptions _options;

    public OpenIdConfigHandler(IOptions<IdpSimulatorOptions> options)
        => _options = options.Value;

    public Task<Result<OpenIdConfigResponse>> Handle(OpenIdConfigQuery request, CancellationToken cancellationToken)
    {
        var issuer = _options.Issuer;
        var response = new OpenIdConfigResponse(
            Issuer: issuer,
            JwksUri: $"{issuer.TrimEnd('/')}{JwksRoute}",
            ResponseTypesSupported: ResponseTypesSupported,
            SubjectTypesSupported: SubjectTypesSupported,
            IdTokenSigningAlgValuesSupported: IdTokenSigningAlgValuesSupported);

        return Task.FromResult(Result.Success(response));
    }
}
