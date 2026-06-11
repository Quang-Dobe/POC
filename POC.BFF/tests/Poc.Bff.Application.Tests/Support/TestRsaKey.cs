namespace Poc.Bff.Application.Tests.Support;

using System.Security.Cryptography;

internal static class TestRsaKey
{
    internal static string NewPrivatePem()
    {
        using var rsa = RSA.Create(2048);
        return rsa.ExportPkcs8PrivateKeyPem();
    }
}
