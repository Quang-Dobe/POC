using Poc.Bff.Application.Common.Dispatching;
using Poc.Bff.Application.Common.Results;

namespace Poc.Bff.Application.Features.Auth.Me;

public sealed class MeHandler : IRequestHandler<MeQuery, Result<MeResponse>>
{
    public Task<Result<MeResponse>> Handle(MeQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var response = new MeResponse(request.DisplayName, request.Roles);
        return Task.FromResult(Result.Success(response));
    }
}
