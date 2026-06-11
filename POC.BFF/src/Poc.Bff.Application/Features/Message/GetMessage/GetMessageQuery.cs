using Poc.Bff.Application.Common.Dispatching;
using Poc.Bff.Application.Common.Results;

namespace Poc.Bff.Application.Features.Message.GetMessage;

public sealed record GetMessageQuery : IRequest<Result<MessageResponse>>;
