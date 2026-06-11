using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Poc.Bff.Api.Errors;
using Poc.Bff.Application.Common.Dispatching;
using Poc.Bff.Application.Features.Message.GetMessage;
using Poc.Bff.Infrastructure.Session;

namespace Poc.Bff.Api.Controllers.V1;

[ApiController]
[Route("api/message")]
public sealed class MessageController : ControllerBase
{
    private readonly ISender _sender;

    public MessageController(ISender sender) => _sender = sender;

    [HttpGet]
    [Authorize(Policy = SessionDefaults.Policy)]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var result = await _sender.Send(new GetMessageQuery(), ct);

        return result.IsSuccess
            ? Ok(result.Value)
            : result.Error.ToProblem();
    }
}
