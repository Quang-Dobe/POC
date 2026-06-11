using FluentValidation;

namespace Poc.Bff.Application.Features.Invites.CreateInvite;

public sealed class CreateInviteCommandValidator : AbstractValidator<CreateInviteCommand>
{
    public CreateInviteCommandValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty()
            .WithMessage("Username must not be empty.");
    }
}
