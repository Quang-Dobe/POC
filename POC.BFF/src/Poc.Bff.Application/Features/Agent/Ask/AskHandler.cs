using Poc.Bff.Application.Abstractions;
using Poc.Bff.Application.Common.Dispatching;
using Poc.Bff.Application.Common.Results;
using Poc.Bff.Domain.Tokens;

namespace Poc.Bff.Application.Features.Agent.Ask;

public sealed class AskHandler : IRequestHandler<AskCommand, Result<AgentResponse>>
{
    private readonly IDownstreamTokenMinter _minter;
    private readonly IAgentGatewayClient _agent;

    public AskHandler(IDownstreamTokenMinter minter, IAgentGatewayClient agent)
    {
        ArgumentNullException.ThrowIfNull(minter);
        ArgumentNullException.ThrowIfNull(agent);

        _minter = minter;
        _agent = agent;
    }

    public async Task<Result<AgentResponse>> Handle(AskCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var claims = new DownstreamClaims(
            Sub: request.Sub,
            Roles: request.Roles,
            Region: request.Region);

        var token = _minter.Mint(claims);

        var response = await _agent
            .AskAsync(request.Question, request.SessionId, token, cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(response);
    }
}
