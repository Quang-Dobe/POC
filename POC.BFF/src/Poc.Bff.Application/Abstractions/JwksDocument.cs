namespace Poc.Bff.Application.Abstractions;

using System.Text.Json.Serialization;
using Microsoft.IdentityModel.Tokens;

public sealed record JwksDocument(
    [property: JsonPropertyName("keys")] IReadOnlyList<JwksKey> Keys)
{
    public static JwksKey ToPublicKey(JsonWebKey key, string kid) => new(
        Kty: JsonWebAlgorithmsKeyTypes.RSA,
        Use: "sig",
        Alg: SecurityAlgorithms.RsaSha256,
        Kid: kid,
        N: key.N,
        E: key.E);
}
