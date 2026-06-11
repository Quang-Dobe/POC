using FluentValidation;

namespace Poc.Bff.Application.Features.Agent.Ask;

public sealed class AskCommandValidator : AbstractValidator<AskCommand>
{
    public AskCommandValidator()
    {
        RuleFor(x => x.Question)
            .NotEmpty()
            .WithMessage("Question must not be empty.");
    }
}
