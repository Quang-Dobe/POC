namespace Api.Endpoints;

using Api.Configuration;
using Microsoft.Extensions.Options;

public static class MessageEndpoints
{
    public static IEndpointRouteBuilder MapMessageEndpoints(this IEndpointRouteBuilder app)
    {

        app.MapGet("/api/message", (IOptions<MessageOptions> messageOptions) =>
                Results.Ok(new { message = messageOptions.Value.DisplayString }))
            .RequireAuthorization();

        return app;
    }
}
