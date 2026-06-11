using Poc.Bff.Application.Common.Dispatching;
using Poc.Bff.Application.Common.Results;

namespace Poc.Bff.Application.Features.Auth.Login;

public sealed record LoginQuery : IRequest<Result<LoginRedirect>>;
