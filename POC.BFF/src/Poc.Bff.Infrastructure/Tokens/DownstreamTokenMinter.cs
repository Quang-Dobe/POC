namespace Poc.Bff.Infrastructure.Tokens;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Poc.Bff.Application.Abstractions;
using Poc.Bff.Application.Configuration;
using Poc.Bff.Domain.Tokens;

public sealed class DownstreamTokenMinter : IDownstreamTokenMinter
{
    private readonly ISigningKeyProvider _signingKeyProvider;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<DownstreamTokenMinter> _logger;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly int _ttlSeconds;

    public DownstreamTokenMinter(
        ISigningKeyProvider signingKeyProvider,
        IOptions<IdpSimulatorOptions> options,
        TimeProvider timeProvider,
        ILogger<DownstreamTokenMinter> logger)
    {
        ArgumentNullException.ThrowIfNull(signingKeyProvider);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);

        _signingKeyProvider = signingKeyProvider;
        _timeProvider = timeProvider;
        _logger = logger;

        var value = options.Value;
        _issuer = value.Issuer;
        _audience = value.Audience;
        _ttlSeconds = value.DownstreamTokenTtlSeconds;
    }

    public string Mint(DownstreamClaims claims) => Mint(claims, _issuer, _audience);

    private string Mint(DownstreamClaims claims, string issuer, string audience)
    {
        ArgumentNullException.ThrowIfNull(claims);

        var (sub, roles, region) = Validate(claims);

        var issuedAt = TruncateToSeconds(_timeProvider.GetUtcNow());
        var expires = issuedAt.AddSeconds(_ttlSeconds);
        var jti = Guid.NewGuid().ToString("N");

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            IssuedAt = issuedAt.UtcDateTime,
            NotBefore = issuedAt.UtcDateTime,
            Expires = expires.UtcDateTime,
            SigningCredentials = _signingKeyProvider.CurrentSigningCredentials(),
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = sub,
                [JwtRegisteredClaimNames.Jti] = jti,
                ["roles"] = roles,
                ["region"] = region,
            },
        };

        var handler = new JsonWebTokenHandler();
        var token = handler.CreateToken(descriptor);

        _logger.LogInformation(
            "Minted downstream token {Jti} for audience {Audience} (kid {Kid}).",
            jti,
            audience,
            _signingKeyProvider.CurrentSigningCredentials().Key.KeyId);

        return token;
    }

    private static (string Sub, string[] Roles, string Region) Validate(DownstreamClaims claims)
    {
        if (string.IsNullOrWhiteSpace(claims.Sub))
        {
            throw new ArgumentException(
                "A downstream mint requires a non-blank subject (sub).",
                nameof(claims));
        }

        if (string.IsNullOrWhiteSpace(claims.Region))
        {
            throw new ArgumentException(
                "A downstream mint requires a non-blank region; the DAB row filter binds to it.",
                nameof(claims));
        }

        if (claims.Roles is not { Count: > 0 })
        {
            throw new ArgumentException(
                "A downstream mint requires at least one role.",
                nameof(claims));
        }

        var roles = new string[claims.Roles.Count];
        for (var i = 0; i < roles.Length; i++)
        {
            var role = claims.Roles[i];
            if (string.IsNullOrWhiteSpace(role))
            {
                throw new ArgumentException(
                    "A downstream mint requires every role to be non-blank.",
                    nameof(claims));
            }

            roles[i] = role;
        }

        return (claims.Sub, roles, claims.Region);
    }

    private static DateTimeOffset TruncateToSeconds(DateTimeOffset value) =>
        new(value.Ticks - (value.Ticks % TimeSpan.TicksPerSecond), value.Offset);
}
