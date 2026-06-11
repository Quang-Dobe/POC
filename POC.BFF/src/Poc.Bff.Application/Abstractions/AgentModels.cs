namespace Poc.Bff.Application.Abstractions;

using System.Text.Json.Serialization;

public record AskRequest(string Question, [property: JsonPropertyName("session_id")] string? SessionId);
public record AgentResponse(
    [property: JsonPropertyName("answer")] string Answer,
    [property: JsonPropertyName("session_id")] string SessionId);
