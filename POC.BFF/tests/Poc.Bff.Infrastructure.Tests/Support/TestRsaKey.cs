namespace Poc.Bff.Infrastructure.Tests.Support;

using System.Security.Cryptography;

internal static class TestRsaKey
{
    internal static string NewPrivatePem()
    {
        using var rsa = RSA.Create(2048);
        return rsa.ExportPkcs8PrivateKeyPem();
    }

    internal static string NewPrivatePemWithHighBitSetModulus(int maxAttempts = 16)
    {
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            var rsa = RSA.Create(2048);
            try
            {
                var parameters = rsa.ExportParameters(includePrivateParameters: false);
                if (parameters.Modulus is { Length: > 0 } modulus && (modulus[0] & 0x80) != 0)
                {
                    return rsa.ExportPkcs8PrivateKeyPem();
                }
            }
            finally
            {
                rsa.Dispose();
            }
        }

        throw new InvalidOperationException(
            $"Could not generate an RSA key with a high-bit-set modulus in {maxAttempts} attempts.");
    }
}
