using Microsoft.AspNetCore.Mvc;
using Poc.Bff.Application.Common.Results;

namespace Poc.Bff.Api.Errors;

public static class ProblemDetailsMapper
{
    public static ObjectResult ToProblem(this Error error)
    {
        var statusCode = error.Type switch
        {
            ErrorType.NotFound   => StatusCodes.Status404NotFound,
            ErrorType.Validation => StatusCodes.Status422UnprocessableEntity,
            ErrorType.Conflict   => StatusCodes.Status409Conflict,
            ErrorType.Upstream   => StatusCodes.Status502BadGateway,
            _                    => StatusCodes.Status500InternalServerError,
        };

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title  = error.Code,
            Detail = error.Message,
        };

        return new ObjectResult(problem) { StatusCode = statusCode };
    }
}
