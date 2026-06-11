using Poc.Bff.Application.Common.Dispatching;
using Poc.Bff.Application.Common.Results;

namespace Poc.Bff.Application.Features.Agent.StartStream;

public sealed record StartAgentStreamCommand(
    string Question,
    string? SessionId,
    string? Sub,
    IReadOnlyList<string> Roles,
    string? Region) : IRequest<Result<IAsyncEnumerable<string>>>;
