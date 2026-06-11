namespace Poc.Bff.Application.Abstractions;

public interface IAgentGatewayClient
{
    Task<AgentResponse> AskAsync(
        string question,
        string? sessionId,
        string token,
        CancellationToken ct = default);

    IAsyncEnumerable<string> AskStreamAsync(
        string question,
        string? sessionId,
        string token,
        CancellationToken ct = default);
}
