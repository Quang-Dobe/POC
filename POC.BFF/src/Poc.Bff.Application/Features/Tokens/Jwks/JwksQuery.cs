using Poc.Bff.Application.Abstractions;
using Poc.Bff.Application.Common.Dispatching;
using Poc.Bff.Application.Common.Results;

namespace Poc.Bff.Application.Features.Tokens.Jwks;

public sealed record JwksQuery : IRequest<Result<JwksDocument>>;
