using System.Security.Cryptography;

namespace Poc.Bff.Integration.Tests.Support;

internal static class TestRsaKey
{
    internal static string NewPrivatePem()
    {
        using var rsa = RSA.Create(2048);
        return rsa.ExportPkcs8PrivateKeyPem();
    }
}
