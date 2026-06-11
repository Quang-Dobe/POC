using FluentAssertions;
using Microsoft.Extensions.Options;
using Poc.Bff.Application.Features.Message;
using Poc.Bff.Application.Features.Message.GetMessage;
using Xunit;

namespace Poc.Bff.Application.Tests.Features.Message;

public class GetMessageHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsConfiguredMessage()
    {
        var options = Options.Create(new MessageOptions { DisplayString = "hello" });
        var handler = new GetMessageHandler(options);

        var result = await handler.Handle(new GetMessageQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Message.Should().Be("hello");
    }
}
