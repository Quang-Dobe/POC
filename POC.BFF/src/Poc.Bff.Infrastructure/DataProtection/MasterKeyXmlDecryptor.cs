namespace Poc.Bff.Infrastructure.DataProtection;

using System.Xml.Linq;
using Poc.Bff.Infrastructure.Secrets;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;
using Microsoft.Extensions.Configuration;

public sealed class MasterKeyXmlDecryptor : IXmlDecryptor
{
    private readonly string _masterSecret;

    public MasterKeyXmlDecryptor(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var configuration = (IConfiguration)services.GetService(typeof(IConfiguration))!
            ?? throw new InvalidOperationException(
                "Cannot decrypt the Data Protection key-ring: no IConfiguration is available.");

        var masterSecret = configuration[SecretStoreConfiguration.DataProtectionMasterKeyKey];
        if (string.IsNullOrEmpty(masterSecret))
        {
            throw new InvalidOperationException(
                "Cannot decrypt the Data Protection key-ring: the master secret " +
                $"'{SecretStoreConfiguration.DataProtectionMasterKeyKey}' is missing from configuration.");
        }

        _masterSecret = masterSecret;
    }

    public XElement Decrypt(XElement encryptedElement)
    {
        ArgumentNullException.ThrowIfNull(encryptedElement);
        return MasterKeyCrypto.Decrypt(_masterSecret, encryptedElement);
    }
}
