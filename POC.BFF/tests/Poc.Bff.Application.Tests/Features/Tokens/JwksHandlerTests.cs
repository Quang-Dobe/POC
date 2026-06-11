using FluentAssertions;
using NSubstitute;
using Poc.Bff.Application.Abstractions;
using Poc.Bff.Application.Features.Tokens.Jwks;
using Xunit;

namespace Poc.Bff.Application.Tests.Features.Tokens;

public class JwksHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsSuccessWithJwksDocument()
    {
        var expectedKey = new JwksKey(
            Kty: "RSA",
            Use: "sig",
            Alg: "RS256",
            Kid: "test-kid-1",
            N: "modulus-base64url",
            E: "AQAB");
        var expectedDocument = new JwksDocument(new[] { expectedKey });

        var provider = Substitute.For<ISigningKeyProvider>();
        provider.PublicJwks().Returns(expectedDocument);

        var handler = new JwksHandler(provider);

        var result = await handler.Handle(new JwksQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Keys.Should().HaveCount(1);
        result.Value.Keys[0].Kid.Should().Be("test-kid-1");
        result.Value.Keys[0].Kty.Should().Be("RSA");
        result.Value.Keys[0].Alg.Should().Be("RS256");
    }
}
