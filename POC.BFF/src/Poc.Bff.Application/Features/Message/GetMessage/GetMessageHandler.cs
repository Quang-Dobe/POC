using Microsoft.Extensions.Options;
using Poc.Bff.Application.Common.Dispatching;
using Poc.Bff.Application.Common.Results;

namespace Poc.Bff.Application.Features.Message.GetMessage;

public sealed class GetMessageHandler : IRequestHandler<GetMessageQuery, Result<MessageResponse>>
{
    private readonly MessageOptions _options;
    public GetMessageHandler(IOptions<MessageOptions> options) => _options = options.Value;

    public Task<Result<MessageResponse>> Handle(GetMessageQuery request, CancellationToken cancellationToken)
        => Task.FromResult(Result.Success(new MessageResponse(_options.DisplayString)));
}
