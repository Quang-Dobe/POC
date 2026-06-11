using Poc.Bff.Application.Abstractions;
using Poc.Bff.Application.Common.Dispatching;
using Poc.Bff.Application.Common.Results;

namespace Poc.Bff.Application.Features.Tokens.Jwks;

public sealed class JwksHandler : IRequestHandler<JwksQuery, Result<JwksDocument>>
{
    private readonly ISigningKeyProvider _signingKeyProvider;

    public JwksHandler(ISigningKeyProvider signingKeyProvider)
        => _signingKeyProvider = signingKeyProvider;

    public Task<Result<JwksDocument>> Handle(JwksQuery request, CancellationToken cancellationToken)
        => Task.FromResult(Result.Success(_signingKeyProvider.PublicJwks()));
}
