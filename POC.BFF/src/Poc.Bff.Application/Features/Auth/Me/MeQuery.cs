using Poc.Bff.Application.Common.Dispatching;
using Poc.Bff.Application.Common.Results;

namespace Poc.Bff.Application.Features.Auth.Me;

public sealed record MeQuery(
    string DisplayName,
    IReadOnlyList<string> Roles) : IRequest<Result<MeResponse>>;
