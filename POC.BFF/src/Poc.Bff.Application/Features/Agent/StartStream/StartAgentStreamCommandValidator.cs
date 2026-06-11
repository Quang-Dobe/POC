using FluentValidation;

namespace Poc.Bff.Application.Features.Agent.StartStream;

public sealed class StartAgentStreamCommandValidator : AbstractValidator<StartAgentStreamCommand>
{
    public StartAgentStreamCommandValidator()
    {
        RuleFor(x => x.Question)
            .NotEmpty()
            .WithMessage("Question must not be empty.");
    }
}
