namespace Poc.Bff.Application.Common;

using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;

public static class Pkce
{
    private const int VerifierByteLength = 32;

    public static string NewCodeVerifier() =>
        Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(VerifierByteLength));

    public static string ComputeChallenge(string codeVerifier)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codeVerifier);
        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));
        return Base64UrlEncoder.Encode(hash);
    }

    public static string NewOpaqueValue() =>
        Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(VerifierByteLength));
}
