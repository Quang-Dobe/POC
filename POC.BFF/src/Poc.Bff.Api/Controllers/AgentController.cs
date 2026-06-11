using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Poc.Bff.Api.Errors;
using Poc.Bff.Application.Abstractions;
using Poc.Bff.Application.Common.Dispatching;
using Poc.Bff.Application.Features.Agent.Ask;
using Poc.Bff.Application.Features.Agent.StartStream;
using Poc.Bff.Infrastructure.Rbac;
using Poc.Bff.Infrastructure.Session;
using Poc.Bff.Infrastructure.Streaming;

namespace Poc.Bff.Api.Controllers;

[ApiController]
public sealed class AgentController : ControllerBase
{
    private const string EventStreamContentType = "text/event-stream";

    private readonly ISender _sender;
    private readonly ILogger _logger;

    public AgentController(
        ISender sender,
        ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        _sender = sender;
        _logger = loggerFactory.CreateLogger("Poc.Bff.Api.Controllers.AgentController");
    }

    [HttpPost("/api/ask")]
    [Authorize(Policy = RbacPolicies.AskPolicy)]
    public async Task<IActionResult> AskAsync([FromBody] AskRequest body, CancellationToken ct)
    {
        var command = new AskCommand(
            Question: body.Question,
            SessionId: body.SessionId,
            Sub: User.FindFirst(SessionDefaults.SubjectClaimType)?.Value,
            Roles: User.FindAll(SessionDefaults.RolesClaimType).Select(c => c.Value).ToArray(),
            Region: User.FindFirst(SessionDefaults.RegionClaimType)?.Value);

        var result = await _sender.Send(command, ct);

        return result.IsSuccess
            ? Ok(result.Value)
            : result.Error.ToProblem();
    }

    [HttpPost("/api/ask/stream")]
    [Authorize(Policy = RbacPolicies.AskPolicy)]
    public async Task StreamAskAsync([FromBody] AskRequest body, CancellationToken ct)
    {
        var command = new StartAgentStreamCommand(
            Question: body.Question,
            SessionId: body.SessionId,
            Sub: User.FindFirst(SessionDefaults.SubjectClaimType)?.Value,
            Roles: User.FindAll(SessionDefaults.RolesClaimType).Select(c => c.Value).ToArray(),
            Region: User.FindFirst(SessionDefaults.RegionClaimType)?.Value);

        var streamResult = await _sender.Send(command, ct);

        if (streamResult.IsFailure)
        {
            Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
            return;
        }

        var enumerator = streamResult.Value.GetAsyncEnumerator(ct);

        bool hasFirst;
        try
        {
            hasFirst = await enumerator.MoveNextAsync().ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            await enumerator.DisposeAsync().ConfigureAwait(false);
            return;
        }
        catch (Exception ex)
        {
            await enumerator.DisposeAsync().ConfigureAwait(false);
            _logger.LogWarning(ex, "Rejected an /api/ask/stream before the first frame: the upstream stream failed.");
            HttpContext.Response.StatusCode = StatusCodes.Status502BadGateway;
            return;
        }

        HttpContext.Response.ContentType = EventStreamContentType;
        var writer = new SseWriter(HttpContext.Response.Body);

        try
        {
            if (hasFirst)
            {
                await writer.WriteMessageAsync(enumerator.Current, ct).ConfigureAwait(false);
                while (true)
                {
                    if (!await enumerator.MoveNextAsync().ConfigureAwait(false))
                        return;

                    await writer.WriteMessageAsync(enumerator.Current, ct).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Terminated an /api/ask/stream after commit: the upstream stream failed mid-flight.");
        }
        finally
        {
            await enumerator.DisposeAsync().ConfigureAwait(false);
        }
    }
}
