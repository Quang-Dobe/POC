namespace Poc.Bff.Infrastructure.Agents;

using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Options;
using Poc.Bff.Application.Abstractions;
using Poc.Bff.Application.Configuration;

public sealed class AgentGatewayClient(HttpClient http, IOptions<AgentOptions> options) : IAgentGatewayClient
{
    private readonly AgentOptions _options = options.Value;

    public async Task<AgentResponse> AskAsync(
        string question,
        string? sessionId,
        string token,
        CancellationToken ct = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(_options.AskTimeoutSeconds));

        var request = BuildRequest("/ask", question, sessionId, token);

        var response = await http.SendAsync(request, timeout.Token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<AgentResponse>(timeout.Token).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Empty response from agent gateway.");
    }

    public async IAsyncEnumerable<string> AskStreamAsync(
        string question,
        string? sessionId,
        string token,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var request = BuildRequest("/ask/stream", question, sessionId, token);

        using var response = await http
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var reader = new StreamReader(stream, System.Text.Encoding.UTF8);

        var chunk = new List<string>();
        while (true)
        {
            ct.ThrowIfCancellationRequested();

            var line = await reader.ReadLineAsync(ct).ConfigureAwait(false);
            if (line is null)
            {
                if (chunk.Count > 0)
                {
                    yield return string.Join('\n', chunk);
                }

                yield break;
            }

            if (line.Length == 0)
            {
                if (chunk.Count > 0)
                {
                    yield return string.Join('\n', chunk);
                    chunk.Clear();
                }

                continue;
            }

            if (line.StartsWith(DataLinePrefix, StringComparison.Ordinal))
            {
                var value = line.Substring(DataLinePrefix.Length);
                if (value.StartsWith(' '))
                {
                    value = value.Substring(1);
                }

                chunk.Add(value);
            }
        }
    }

    private static HttpRequestMessage BuildRequest(
        string path, string question, string? sessionId, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Headers.Authorization = new("Bearer", token);
        request.Content = JsonContent.Create(new { question, session_id = sessionId });
        return request;
    }

    private const string DataLinePrefix = "data:";
}
