namespace Poc.Bff.Infrastructure.Tokens;

using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Poc.Bff.Application.Abstractions;
using Poc.Bff.Application.Configuration;

public sealed class SigningKeyProvider : ISigningKeyProvider
{
    private readonly RsaSecurityKey _currentKey;
    private readonly RsaSecurityKey? _nextKey;
    private readonly JwksDocument _publicJwks;

    public SigningKeyProvider(
        IOptions<IdpSimulatorOptions> options,
        ILogger<SigningKeyProvider> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        var value = options.Value;

        _currentKey = ImportKey(value.SigningKeyId, value.SigningKeyPem, isCurrent: true);

        var hasNextId = !string.IsNullOrWhiteSpace(value.NextSigningKeyId);
        var hasNextPem = !string.IsNullOrWhiteSpace(value.NextSigningKeyPem);
        _nextKey = hasNextId && hasNextPem
            ? ImportKey(value.NextSigningKeyId!, value.NextSigningKeyPem!, isCurrent: false)
            : null;

        _publicJwks = BuildPublicJwks(_currentKey, _nextKey);

        if (_nextKey is null)
        {
            logger.LogInformation(
                "IDP-Simulator signing key loaded. Current kid {CurrentKid}; no next key configured.",
                _currentKey.KeyId);
        }
        else
        {
            logger.LogInformation(
                "IDP-Simulator signing keys loaded. Current kid {CurrentKid}; next kid {NextKid} " +
                "published for overlap (not used to sign - F1).",
                _currentKey.KeyId,
                _nextKey.KeyId);
        }
    }

    public SigningCredentials CurrentSigningCredentials() =>
        new(_currentKey, SecurityAlgorithms.RsaSha256);

    public JwksDocument PublicJwks() => _publicJwks;

    private static RsaSecurityKey ImportKey(string keyId, string? pem, bool isCurrent)
    {
        if (string.IsNullOrWhiteSpace(pem))
        {
            throw new InvalidOperationException(
                $"The {(isCurrent ? "current" : "next")} IDP-Simulator signing key PEM is missing " +
                $"for kid '{keyId}'. It must be supplied through the secret store.");
        }

        var rsa = RSA.Create();
        try
        {
            rsa.ImportFromPem(pem);
        }
        catch (Exception ex) when (ex is ArgumentException or CryptographicException)
        {
            rsa.Dispose();
            throw new InvalidOperationException(
                $"The {(isCurrent ? "current" : "next")} IDP-Simulator signing key PEM for kid " +
                $"'{keyId}' is not a valid RSA PKCS#8/PKCS#1 PEM.",
                ex);
        }

        return new RsaSecurityKey(rsa) { KeyId = keyId };
    }

    private static JwksDocument BuildPublicJwks(RsaSecurityKey current, RsaSecurityKey? next)
    {
        var keys = new List<JwksKey> { ToPublicJwksKey(current, current.KeyId!) };
        if (next is not null)
        {
            keys.Add(ToPublicJwksKey(next, next.KeyId!));
        }

        return new JwksDocument(keys);
    }

    private static JwksKey ToPublicJwksKey(RsaSecurityKey key, string kid)
    {
        var rsa = key.Rsa ?? throw new InvalidOperationException(
            $"RSA signing key '{kid}' exposes no RSA instance to project.");

        var publicParameters = rsa.ExportParameters(includePrivateParameters: false);
        using var publicRsa = RSA.Create(publicParameters);
        var publicKey = new RsaSecurityKey(publicRsa) { KeyId = kid };

        var jwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(publicKey);
        return JwksDocument.ToPublicKey(jwk, kid);
    }
}
