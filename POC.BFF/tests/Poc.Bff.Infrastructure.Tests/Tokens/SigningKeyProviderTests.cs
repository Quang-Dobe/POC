namespace Poc.Bff.Infrastructure.Tests.Tokens;

using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Poc.Bff.Application.Configuration;
using Poc.Bff.Infrastructure.Tests.Support;
using Poc.Bff.Infrastructure.Tokens;
using Xunit;

public class SigningKeyProviderTests
{
    private const string CurrentKid = "poc-idp-key-1";
    private const string NextKid = "poc-idp-key-2";

    private static SigningKeyProvider BuildProvider(
        string currentPem,
        string? nextPem = null,
        string currentKid = CurrentKid,
        string? nextKid = null)
    {
        var options = Options.Create(new IdpSimulatorOptions
        {
            Issuer = "https://localhost:5001",
            Audience = "poc-internal",
            DownstreamTokenTtlSeconds = 300,
            SigningKeyId = currentKid,
            SigningKeyPem = currentPem,
            NextSigningKeyId = nextKid,
            NextSigningKeyPem = nextPem,
        });

        return new SigningKeyProvider(options, NullLogger<SigningKeyProvider>.Instance);
    }

    [Fact]
    public void CurrentSigningCredentials_BindsConfiguredKidAndRs256()
    {
        var provider = BuildProvider(TestRsaKey.NewPrivatePem());

        var credentials = provider.CurrentSigningCredentials();

        Assert.Equal(SecurityAlgorithms.RsaSha256, credentials.Algorithm);
        Assert.Equal(CurrentKid, credentials.Key.KeyId);
        Assert.IsType<RsaSecurityKey>(credentials.Key);
    }

    [Fact]
    public void CurrentSigningCredentials_KeyRetainsPrivateMaterial_ForSigning()
    {
        var provider = BuildProvider(TestRsaKey.NewPrivatePem());

        var key = Assert.IsType<RsaSecurityKey>(provider.CurrentSigningCredentials().Key);

        var privateParameters = key.Rsa!.ExportParameters(includePrivateParameters: true);
        Assert.NotNull(privateParameters.D);
    }

    [Fact]
    public void PublicJwks_EmitsExactConfiguredKid()
    {
        var provider = BuildProvider(TestRsaKey.NewPrivatePem());

        var jwks = provider.PublicJwks();

        var key = Assert.Single(jwks.Keys);
        Assert.Equal(CurrentKid, key.Kid);
    }

    [Fact]
    public void PublicJwks_EmitsRsaSigRs256Metadata()
    {
        var provider = BuildProvider(TestRsaKey.NewPrivatePem());

        var key = Assert.Single(provider.PublicJwks().Keys);

        Assert.Equal(JsonWebAlgorithmsKeyTypes.RSA, key.Kty);
        Assert.Equal("sig", key.Use);
        Assert.Equal(SecurityAlgorithms.RsaSha256, key.Alg);
    }

    [Fact]
    public void PublicJwks_SerializedShape_ContainsNoPrivateParameters()
    {
        var provider = BuildProvider(TestRsaKey.NewPrivatePem());

        var json = JsonSerializer.Serialize(provider.PublicJwks());

        using var document = JsonDocument.Parse(json);
        var key = Assert.Single(document.RootElement.GetProperty("keys").EnumerateArray());

        Assert.True(key.TryGetProperty("n", out _));
        Assert.True(key.TryGetProperty("e", out _));
        foreach (var privateMember in new[] { "d", "p", "q", "dp", "dq", "qi", "oth" })
        {
            Assert.False(
                key.TryGetProperty(privateMember, out _),
                $"JWKS must not leak the private parameter '{privateMember}'.");
        }
    }

    [Fact]
    public void PublicJwks_ModulusAndExponent_RoundTripToTheSamePublicKey()
    {
        var pem = TestRsaKey.NewPrivatePem();
        using var source = RSA.Create();
        source.ImportFromPem(pem);
        var sourcePublic = source.ExportParameters(includePrivateParameters: false);

        var key = Assert.Single(BuildProvider(pem).PublicJwks().Keys);

        var n = Base64UrlEncoder.DecodeBytes(key.N);
        var e = Base64UrlEncoder.DecodeBytes(key.E);

        Assert.Equal(TrimLeadingZeros(sourcePublic.Modulus!), TrimLeadingZeros(n));
        Assert.Equal(TrimLeadingZeros(sourcePublic.Exponent!), TrimLeadingZeros(e));
    }

    [Fact]
    public void PublicJwks_IsConsumableByIndependentJwkParser()
    {
        var provider = BuildProvider(TestRsaKey.NewPrivatePem());

        var json = JsonSerializer.Serialize(provider.PublicJwks());

        var parsed = new JsonWebKeySet(json);

        var jwk = Assert.Single(parsed.Keys);
        Assert.Equal(CurrentKid, jwk.Kid);
        Assert.Equal(JsonWebAlgorithmsKeyTypes.RSA, jwk.Kty);
        Assert.False(string.IsNullOrEmpty(jwk.N));
        Assert.False(string.IsNullOrEmpty(jwk.E));
        Assert.Null(jwk.D);
        Assert.Null(jwk.P);
        Assert.Null(jwk.Q);

        var signingKey = Assert.Single(parsed.GetSigningKeys());
        Assert.IsType<RsaSecurityKey>(signingKey);
        Assert.Equal(CurrentKid, signingKey.KeyId);
    }

    [Fact]
    public void PublicJwks_HighBitSetModulus_IsBase64UrlEncodedWithNoLeadingZeroPadByte()
    {
        var pem = TestRsaKey.NewPrivatePemWithHighBitSetModulus();
        using var source = RSA.Create();
        source.ImportFromPem(pem);
        var sourceModulus = source.ExportParameters(includePrivateParameters: false).Modulus!;
        Assert.NotEqual(0x00, sourceModulus[0]);

        var provider = BuildProvider(pem);
        var json = JsonSerializer.Serialize(provider.PublicJwks());
        var key = Assert.Single(provider.PublicJwks().Keys);

        var decoded = Base64UrlEncoder.DecodeBytes(key.N);
        Assert.NotEqual(0x00, decoded[0]);
        Assert.Equal(sourceModulus, decoded);

        var parsed = new JsonWebKeySet(json);
        var signingKey = Assert.Single(parsed.GetSigningKeys());
        Assert.IsType<RsaSecurityKey>(signingKey);
    }

    [Fact]
    public void PublicJwks_WithNextKeyConfigured_EmitsCurrentAndNextPublicKeys()
    {
        var provider = BuildProvider(
            currentPem: TestRsaKey.NewPrivatePem(),
            nextPem: TestRsaKey.NewPrivatePem(),
            nextKid: NextKid);

        var jwks = provider.PublicJwks();

        Assert.Equal(2, jwks.Keys.Count);
        Assert.Contains(jwks.Keys, k => k.Kid == CurrentKid);
        Assert.Contains(jwks.Keys, k => k.Kid == NextKid);
        var json = JsonSerializer.Serialize(jwks);
        Assert.DoesNotContain("\"d\":", json);
    }

    [Fact]
    public void PublicJwks_WithoutNextKey_EmitsOnlyTheCurrentKey()
    {
        var provider = BuildProvider(TestRsaKey.NewPrivatePem());

        Assert.Single(provider.PublicJwks().Keys);
    }

    [Fact]
    public void Constructor_WithInvalidPem_ThrowsWithoutEchoingTheKeyMaterial()
    {
        const string garbage = "-----BEGIN PRIVATE KEY-----\nnot-a-real-key\n-----END PRIVATE KEY-----";

        var ex = Assert.Throws<InvalidOperationException>(() => BuildProvider(garbage));

        Assert.Contains(CurrentKid, ex.Message);
        Assert.DoesNotContain("not-a-real-key", ex.Message);
    }

    private static byte[] TrimLeadingZeros(byte[] value)
    {
        var start = 0;
        while (start < value.Length - 1 && value[start] == 0x00)
        {
            start++;
        }

        return value[start..];
    }
}
