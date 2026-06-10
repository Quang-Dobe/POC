namespace Api.Endpoints;

using Api.Agents;

public static class AgentEndpoints
{
    public static IEndpointRouteBuilder MapAgentEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/ask", async (
            AskRequest body,
            IAgentGatewayClient agent,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            var auth = httpContext.Request.Headers.Authorization.ToString();
            var bearer = auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                ? auth["Bearer ".Length..].Trim()
                : string.Empty;

            var result = await agent.AskAsync(body.Question, body.SessionId, bearer, ct);
            return Results.Ok(result);
        })
        .RequireAuthorization();

        return app;
    }
}
