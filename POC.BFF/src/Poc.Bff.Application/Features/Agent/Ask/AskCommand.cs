using Poc.Bff.Application.Abstractions;
using Poc.Bff.Application.Common.Dispatching;
using Poc.Bff.Application.Common.Results;

namespace Poc.Bff.Application.Features.Agent.Ask;

public sealed record AskCommand(
    string Question,
    string? SessionId,
    string? Sub,
    IReadOnlyList<string> Roles,
    string? Region) : IRequest<Result<AgentResponse>>;
