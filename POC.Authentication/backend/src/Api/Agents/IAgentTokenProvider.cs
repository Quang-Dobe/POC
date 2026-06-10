namespace Api.Agents;

public interface IAgentTokenProvider
{
    Task<string> GetTokenAsync(string userToken, CancellationToken ct = default);
}
