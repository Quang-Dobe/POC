namespace Api.Agents;

public interface IAgentGatewayClient
{
    Task<AgentResponse> AskAsync(
        string question,
        string? sessionId,
        string userToken,
        CancellationToken ct = default);
}
