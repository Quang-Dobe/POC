using Poc.Bff.Application.Common.Dispatching;
using Poc.Bff.Application.Common.Results;

namespace Poc.Bff.Application.Features.Auth.Callback;

public sealed record CallbackCommand(
    string Code,
    string State,
    string ExpectedState,
    string CodeVerifier,
    string Nonce) : IRequest<Result<SessionEstablished>>;
