using FluentValidation;

namespace Poc.Bff.Application.Features.Auth.Callback;

public sealed class CallbackCommandValidator : AbstractValidator<CallbackCommand>
{
    public CallbackCommandValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty()
            .WithMessage("Authorization code must not be empty.");

        RuleFor(x => x.State)
            .NotEmpty()
            .WithMessage("State parameter must not be empty.");
    }
}
