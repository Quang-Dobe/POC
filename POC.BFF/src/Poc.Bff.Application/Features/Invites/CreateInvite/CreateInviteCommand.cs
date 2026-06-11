using Poc.Bff.Application.Common.Dispatching;
using Poc.Bff.Application.Common.Results;

namespace Poc.Bff.Application.Features.Invites.CreateInvite;

public sealed record CreateInviteCommand(
    string Username,
    string? DisplayName = null) : IRequest<Result<CreateInviteResponse>>;
