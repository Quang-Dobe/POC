using FluentAssertions;
using FluentValidation;
using Poc.Bff.Application.Common.Behaviors;
using Poc.Bff.Application.Common.Dispatching;
using Poc.Bff.Application.Common.Results;
using Xunit;

namespace Poc.Bff.Application.Tests.Common;

public class ValidationBehaviorTests
{
    public sealed record Cmd(string Name) : IRequest<Result>;
    public sealed class CmdValidator : AbstractValidator<Cmd>
    {
        public CmdValidator() => RuleFor(x => x.Name).NotEmpty();
    }

    [Fact]
    public async Task Invalid_ShortCircuits_WithValidationFailure()
    {
        var behavior = new ValidationPipelineBehavior<Cmd, Result>(
            new[] { new CmdValidator() });

        var result = await behavior.Handle(
            new Cmd(""),
            () => Task.FromResult(Result.Success()),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
    }
}
