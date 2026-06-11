namespace Poc.Bff.Infrastructure.DataProtection;

using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;

public sealed class MasterKeyXmlEncryptor : IXmlEncryptor
{
    private readonly string _masterSecret;

    public MasterKeyXmlEncryptor(string masterSecret)
    {
        if (string.IsNullOrEmpty(masterSecret))
        {
            throw new ArgumentException(
                "The Data Protection master secret must be supplied; refusing to write an " +
                "unencrypted key-ring to Redis.",
                nameof(masterSecret));
        }

        _masterSecret = masterSecret;
    }

    public EncryptedXmlInfo Encrypt(XElement plaintextElement)
    {
        ArgumentNullException.ThrowIfNull(plaintextElement);

        var encrypted = MasterKeyCrypto.Encrypt(_masterSecret, plaintextElement);
        return new EncryptedXmlInfo(encrypted, typeof(MasterKeyXmlDecryptor));
    }
}
