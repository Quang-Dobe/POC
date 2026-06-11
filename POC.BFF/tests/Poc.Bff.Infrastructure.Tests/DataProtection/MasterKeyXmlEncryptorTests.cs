namespace Poc.Bff.Infrastructure.Tests.DataProtection;

using System;
using System.Security.Cryptography;
using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Poc.Bff.Infrastructure.DataProtection;
using Xunit;

public class MasterKeyXmlEncryptorTests
{
    private const string Master = "a-test-master-secret-from-the-store";

    private static XElement SampleKeyElement() =>
        new("key",
            new XAttribute("id", "11111111-1111-1111-1111-111111111111"),
            new XElement("creationDate", "2026-06-12T00:00:00Z"),
            new XElement("descriptor", new XElement("secret", "super-secret-key-material")));

    private static MasterKeyXmlDecryptor NewDecryptor(string master)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DataProtection:MasterKey"] = master,
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        return new MasterKeyXmlDecryptor(services.BuildServiceProvider());
    }

    [Fact]
    public void Encrypt_then_Decrypt_round_trips_the_original_element()
    {
        var encryptor = new MasterKeyXmlEncryptor(Master);
        var decryptor = NewDecryptor(Master);
        var original = SampleKeyElement();

        EncryptedXmlInfo info = encryptor.Encrypt(original);
        var roundTripped = decryptor.Decrypt(info.EncryptedElement);

        Assert.True(XNode.DeepEquals(original, roundTripped));
        Assert.Equal("encryptedKey", info.EncryptedElement.Name.LocalName);
        Assert.DoesNotContain("super-secret-key-material", info.EncryptedElement.ToString(), StringComparison.Ordinal);
        Assert.Equal(typeof(MasterKeyXmlDecryptor), info.DecryptorType);
    }

    [Fact]
    public void Decrypt_rejects_a_tampered_ciphertext_with_a_loud_failure()
    {
        var encryptor = new MasterKeyXmlEncryptor(Master);
        var decryptor = NewDecryptor(Master);

        var info = encryptor.Encrypt(SampleKeyElement());

        var frame = Convert.FromBase64String(info.EncryptedElement.Value);
        frame[^1] ^= 0xFF;
        var tampered = new XElement(info.EncryptedElement.Name, Convert.ToBase64String(frame));

        Assert.Throws<AuthenticationTagMismatchException>(() => decryptor.Decrypt(tampered));
    }

    [Fact]
    public void Encrypt_with_the_same_master_on_independent_instances_decrypts_cross_instance()
    {
        var encryptorA = new MasterKeyXmlEncryptor(Master);
        var decryptorB = NewDecryptor(Master);
        var original = SampleKeyElement();

        var info = encryptorA.Encrypt(original);
        var roundTripped = decryptorB.Decrypt(info.EncryptedElement);

        Assert.True(XNode.DeepEquals(original, roundTripped));
    }

    [Fact]
    public void Decrypt_with_a_different_master_fails()
    {
        var encryptor = new MasterKeyXmlEncryptor(Master);
        var wrongDecryptor = NewDecryptor("a-completely-different-master-secret");

        var info = encryptor.Encrypt(SampleKeyElement());

        Assert.Throws<AuthenticationTagMismatchException>(() => wrongDecryptor.Decrypt(info.EncryptedElement));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Constructor_fails_loud_when_the_master_is_missing(string? master)
    {
        Assert.Throws<ArgumentException>(() => new MasterKeyXmlEncryptor(master!));
    }
}
