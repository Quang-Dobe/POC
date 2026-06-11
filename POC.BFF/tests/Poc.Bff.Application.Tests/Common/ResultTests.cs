using FluentAssertions;
using Poc.Bff.Application.Common.Results;
using Xunit;

namespace Poc.Bff.Application.Tests.Common;

public class ResultTests
{
    [Fact]
    public void Success_CarriesNoneError()
    {
        var result = Result.Success();
        result.IsSuccess.Should().BeTrue();
        result.Error.Should().Be(Error.None);
    }

    [Fact]
    public void Failure_WithNoneError_Throws()
    {
        var act = () => Result.Failure(Error.None);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void GenericFailure_AccessingValue_Throws()
    {
        var result = Result.Failure<int>(new Error("X", "msg", ErrorType.NotFound));
        result.IsFailure.Should().BeTrue();
        var act = () => _ = result.Value;
        act.Should().Throw<InvalidOperationException>();
    }
}
