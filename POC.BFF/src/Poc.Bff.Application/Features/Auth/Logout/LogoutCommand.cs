using Poc.Bff.Application.Common.Dispatching;
using Poc.Bff.Application.Common.Results;

namespace Poc.Bff.Application.Features.Auth.Logout;

public sealed record LogoutCommand : IRequest<Result>;
