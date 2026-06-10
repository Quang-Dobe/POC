namespace Api.Agents;

using System.Net.Http.Json;

public class AgentGatewayClient(HttpClient http, IAgentTokenProvider tokenProvider)
    : IAgentGatewayClient
{
    public async Task<AgentResponse> AskAsync(
        string question,
        string? sessionId,
        string userToken,
        CancellationToken ct = default)
    {
        var agentToken = await tokenProvider.GetTokenAsync(userToken, ct);

        var request = new HttpRequestMessage(HttpMethod.Post, "/ask");
        request.Headers.Authorization = new("Bearer", agentToken);
        request.Content = JsonContent.Create(new { question, session_id = sessionId });

        var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<AgentResponse>(ct)
            ?? throw new InvalidOperationException("Empty response from agent gateway.");
    }
}
