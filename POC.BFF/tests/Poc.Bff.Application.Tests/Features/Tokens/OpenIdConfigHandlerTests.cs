using FluentAssertions;
using Microsoft.Extensions.Options;
using Poc.Bff.Application.Configuration;
using Poc.Bff.Application.Features.Tokens.OpenIdConfiguration;
using Xunit;

namespace Poc.Bff.Application.Tests.Features.Tokens;

public class OpenIdConfigHandlerTests
{
    private static readonly IdpSimulatorOptions DefaultOptions = new()
    {
        Issuer = "https://idp.example.com",
        Audience = "bff-api",
        DownstreamTokenTtlSeconds = 300,
        SigningKeyId = "key-1"
    };

    [Fact]
    public async Task Handle_ReturnsSuccessWithCorrectIssuer()
    {
        var handler = BuildHandler(DefaultOptions);

        var result = await handler.Handle(new OpenIdConfigQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Issuer.Should().Be("https://idp.example.com");
    }

    [Fact]
    public async Task Handle_BuildsJwksUriFromIssuer()
    {
        var handler = BuildHandler(DefaultOptions);

        var result = await handler.Handle(new OpenIdConfigQuery(), CancellationToken.None);

        result.Value.JwksUri.Should().Be("https://idp.example.com/.well-known/jwks.json");
    }

    [Fact]
    public async Task Handle_JwksUri_TrimsTrailingSlashFromIssuer()
    {
        var options = new IdpSimulatorOptions
        {
            Issuer = "https://idp.example.com/",
            Audience = "bff-api",
            DownstreamTokenTtlSeconds = 300,
            SigningKeyId = "key-1"
        };
        var handler = BuildHandler(options);

        var result = await handler.Handle(new OpenIdConfigQuery(), CancellationToken.None);

        result.Value.JwksUri.Should().Be("https://idp.example.com/.well-known/jwks.json");
    }

    [Fact]
    public async Task Handle_ResponseTypesSupported_ContainsIdToken()
    {
        var handler = BuildHandler(DefaultOptions);

        var result = await handler.Handle(new OpenIdConfigQuery(), CancellationToken.None);

        result.Value.ResponseTypesSupported.Should().Contain("id_token");
    }

    [Fact]
    public async Task Handle_SubjectTypesSupported_ContainsPublic()
    {
        var handler = BuildHandler(DefaultOptions);

        var result = await handler.Handle(new OpenIdConfigQuery(), CancellationToken.None);

        result.Value.SubjectTypesSupported.Should().Contain("public");
    }

    [Fact]
    public async Task Handle_IdTokenSigningAlgValuesSupported_ContainsRs256()
    {
        var handler = BuildHandler(DefaultOptions);

        var result = await handler.Handle(new OpenIdConfigQuery(), CancellationToken.None);

        result.Value.IdTokenSigningAlgValuesSupported.Should().Contain("RS256");
    }

    private static OpenIdConfigHandler BuildHandler(IdpSimulatorOptions options)
        => new(Options.Create(options));
}
