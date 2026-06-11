using Poc.Bff.Application.Common.Dispatching;
using Poc.Bff.Application.Common.Results;

namespace Poc.Bff.Application.Features.Tokens.OpenIdConfiguration;

public sealed record OpenIdConfigQuery : IRequest<Result<OpenIdConfigResponse>>;
