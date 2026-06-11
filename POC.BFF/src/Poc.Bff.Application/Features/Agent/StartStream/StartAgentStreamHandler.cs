using Poc.Bff.Application.Abstractions;
using Poc.Bff.Application.Common.Dispatching;
using Poc.Bff.Application.Common.Results;
using Poc.Bff.Domain.Tokens;

namespace Poc.Bff.Application.Features.Agent.StartStream;

public sealed class StartAgentStreamHandler : IRequestHandler<StartAgentStreamCommand, Result<IAsyncEnumerable<string>>>
{
    private readonly IDownstreamTokenMinter _minter;
    private readonly IAgentGatewayClient _agent;

    public StartAgentStreamHandler(IDownstreamTokenMinter minter, IAgentGatewayClient agent)
    {
        ArgumentNullException.ThrowIfNull(minter);
        ArgumentNullException.ThrowIfNull(agent);
        _minter = minter;
        _agent = agent;
    }

    public Task<Result<IAsyncEnumerable<string>>> Handle(StartAgentStreamCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var claims = new DownstreamClaims(
            Sub: request.Sub,
            Roles: request.Roles,
            Region: request.Region);

        var mintedToken = _minter.Mint(claims);

        var stream = _agent.AskStreamAsync(request.Question, request.SessionId, mintedToken, cancellationToken);

        return Task.FromResult(Result.Success(stream));
    }
}
