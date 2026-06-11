namespace Poc.Bff.Infrastructure.Tests.Tokens;

using System;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Poc.Bff.Application.Configuration;
using Poc.Bff.Domain.Tokens;
using Poc.Bff.Infrastructure.Tests.Support;
using Poc.Bff.Infrastructure.Tokens;
using Xunit;

public class DownstreamTokenMinterTests
{
    private const string Issuer = "https://localhost:5001";
    private const string Audience = "poc-internal";
    private const string SigningKid = "poc-idp-key-1";
    private const int TtlSeconds = 300;

    private static readonly DateTimeOffset FixedNow =
        new DateTimeOffset(2026, 6, 11, 12, 0, 0, TimeSpan.Zero).AddMilliseconds(750);

    private static DownstreamClaims ManagerClaims() =>
        new("user-123", new[] { "manager" }, "Oslo");

    private static (DownstreamTokenMinter Minter, SigningKeyProvider KeyProvider, FakeTimeProvider Clock)
        BuildMinter(string? pem = null)
    {
        pem ??= TestRsaKey.NewPrivatePem();

        var options = Options.Create(new IdpSimulatorOptions
        {
            Issuer = Issuer,
            Audience = Audience,
            DownstreamTokenTtlSeconds = TtlSeconds,
            SigningKeyId = SigningKid,
            SigningKeyPem = pem,
        });

        var keyProvider = new SigningKeyProvider(options, NullLogger<SigningKeyProvider>.Instance);
        var clock = new FakeTimeProvider(FixedNow);
        var minter = new DownstreamTokenMinter(
            keyProvider,
            options,
            clock,
            NullLogger<DownstreamTokenMinter>.Instance);

        return (minter, keyProvider, clock);
    }

    private static JsonElement DecodePayload(string token)
    {
        var segments = token.Split('.');
        Assert.Equal(3, segments.Length);
        var payloadJson = Encoding.UTF8.GetString(Base64UrlEncoder.DecodeBytes(segments[1]));
        return JsonDocument.Parse(payloadJson).RootElement.Clone();
    }

    [Fact]
    public void Mint_SetsSharedIssuerAudienceAndCarriesRolesAndRegion()
    {
        var (minter, _, _) = BuildMinter();

        var token = minter.Mint(ManagerClaims());

        var payload = DecodePayload(token);
        Assert.Equal(Issuer, payload.GetProperty("iss").GetString());
        Assert.Equal(Audience, payload.GetProperty("aud").GetString());
        Assert.Equal("user-123", payload.GetProperty("sub").GetString());
        Assert.Equal("Oslo", payload.GetProperty("region").GetString());
        Assert.Equal(JsonValueKind.Array, payload.GetProperty("roles").ValueKind);
        Assert.Equal("manager", payload.GetProperty("roles")[0].GetString());
    }

    [Fact]
    public void Mint_ExpEqualsIatPlusTtl_WithWholeSecondTruncation()
    {
        var (minter, _, _) = BuildMinter();

        var payload = DecodePayload(minter.Mint(ManagerClaims()));

        var iat = payload.GetProperty("iat").GetInt64();
        var exp = payload.GetProperty("exp").GetInt64();

        var expectedIat = FixedNow.AddMilliseconds(-750).ToUnixTimeSeconds();
        Assert.Equal(expectedIat, iat);
        Assert.Equal(iat + TtlSeconds, exp);
    }

    [Fact]
    public void Mint_HeaderKid_MatchesConfiguredSigningKeyId()
    {
        var (minter, _, _) = BuildMinter();

        var jwt = new JsonWebToken(minter.Mint(ManagerClaims()));

        Assert.Equal(SigningKid, jwt.Kid);
        Assert.Equal(SecurityAlgorithms.RsaSha256, jwt.Alg);
    }

    [Fact]
    public void Mint_EmitsAJtiPerMint_ThatIsUnique()
    {
        var (minter, _, _) = BuildMinter();

        var first = DecodePayload(minter.Mint(ManagerClaims()));
        var second = DecodePayload(minter.Mint(ManagerClaims()));

        var firstJti = first.GetProperty("jti").GetString();
        var secondJti = second.GetProperty("jti").GetString();

        Assert.False(string.IsNullOrWhiteSpace(firstJti));
        Assert.False(string.IsNullOrWhiteSpace(secondJti));
        Assert.NotEqual(firstJti, secondJti);
    }

    [Fact]
    public void Mint_SingleRole_StillSerializesRolesAsAJsonArray()
    {
        var (minter, _, _) = BuildMinter();

        var rolesElement = DecodePayload(minter.Mint(ManagerClaims())).GetProperty("roles");

        Assert.Equal(JsonValueKind.Array, rolesElement.ValueKind);
        Assert.Equal(1, rolesElement.GetArrayLength());
        Assert.Equal("manager", rolesElement[0].GetString());
    }

    [Theory]
    [InlineData("manager")]
    [InlineData("reader")]
    public void Mint_RoleZero_ReflectsTheInputRole(string role)
    {
        var (minter, _, _) = BuildMinter();

        var payload = DecodePayload(
            minter.Mint(new DownstreamClaims("user-123", new[] { role }, "Oslo")));

        Assert.Equal(role, payload.GetProperty("roles")[0].GetString());
    }

    [Fact]
    public async Task Mint_ValidatesAgainstTheStepBPublicJwks_AndCarriesRegion()
    {
        var (minter, keyProvider, _) = BuildMinter();
        var token = minter.Mint(ManagerClaims());

        var publicKeys = new JsonWebKeySet(JsonSerializer.Serialize(keyProvider.PublicJwks()))
            .GetSigningKeys();

        var parameters = new TokenValidationParameters
        {
            ValidIssuer = Issuer,
            ValidAudience = Audience,
            IssuerSigningKeys = publicKeys,
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = false,
        };

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token, parameters);

        Assert.True(result.IsValid, result.Exception?.Message);
        Assert.Equal(Issuer, result.Claims["iss"]);
        Assert.Equal(Audience, result.Claims["aud"]);
        Assert.Equal("Oslo", result.Claims["region"]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Mint_BlankSubject_Throws(string? sub)
    {
        var (minter, _, _) = BuildMinter();
        var claims = new DownstreamClaims(sub, new[] { "manager" }, "Oslo");

        Assert.Throws<ArgumentException>(() => minter.Mint(claims));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Mint_BlankRegion_Throws(string? region)
    {
        var (minter, _, _) = BuildMinter();
        var claims = new DownstreamClaims("user-123", new[] { "manager" }, region);

        Assert.Throws<ArgumentException>(() => minter.Mint(claims));
    }

    [Fact]
    public void Mint_NullRoles_Throws()
    {
        var (minter, _, _) = BuildMinter();
        var claims = new DownstreamClaims("user-123", null, "Oslo");

        Assert.Throws<ArgumentException>(() => minter.Mint(claims));
    }

    [Fact]
    public void Mint_EmptyRoles_Throws()
    {
        var (minter, _, _) = BuildMinter();
        var claims = new DownstreamClaims("user-123", Array.Empty<string>(), "Oslo");

        Assert.Throws<ArgumentException>(() => minter.Mint(claims));
    }

    [Fact]
    public void Mint_BlankRoleEntry_Throws()
    {
        var (minter, _, _) = BuildMinter();
        var claims = new DownstreamClaims("user-123", new[] { "manager", "  " }, "Oslo");

        Assert.Throws<ArgumentException>(() => minter.Mint(claims));
    }
}
