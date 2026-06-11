using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Poc.Bff.Api.Errors;
using Poc.Bff.Application.Common.Dispatching;
using Poc.Bff.Application.Features.Tokens.Jwks;

namespace Poc.Bff.Api.Controllers.WellKnown;

[ApiController]
[AllowAnonymous]
[Route(".well-known/jwks.json")]
public sealed class JwksController : ControllerBase
{
    private readonly ISender _sender;

    public JwksController(ISender sender) => _sender = sender;

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var result = await _sender.Send(new JwksQuery(), ct);

        return result.IsSuccess
            ? Ok(result.Value)
            : result.Error.ToProblem();
    }
}
