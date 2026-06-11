namespace Poc.Bff.Infrastructure.DataProtection;

using System.Security.Cryptography;
using System.Xml.Linq;

internal static class MasterKeyCrypto
{
    private const string InfoLabel = "poc-dp-keyring-v1";

    private const int NonceByteLength = 12;
    private const int TagByteLength = 16;
    private const int KeyByteLength = 32;

    internal static readonly XName EncryptedKeyElementName =
        XName.Get("encryptedKey", "https://poc.local/dataprotection/masterkey");

    internal static XElement Encrypt(string masterSecret, XElement plaintextElement)
    {
        ArgumentException.ThrowIfNullOrEmpty(masterSecret);
        ArgumentNullException.ThrowIfNull(plaintextElement);

        var key = DeriveKey(masterSecret);
        var plaintext = System.Text.Encoding.UTF8.GetBytes(plaintextElement.ToString(SaveOptions.DisableFormatting));

        var nonce = RandomNumberGenerator.GetBytes(NonceByteLength);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagByteLength];

        using (var aesGcm = new AesGcm(key, TagByteLength))
        {
            aesGcm.Encrypt(nonce, plaintext, ciphertext, tag);
        }

        var frame = new byte[NonceByteLength + TagByteLength + ciphertext.Length];
        Buffer.BlockCopy(nonce, 0, frame, 0, NonceByteLength);
        Buffer.BlockCopy(tag, 0, frame, NonceByteLength, TagByteLength);
        Buffer.BlockCopy(ciphertext, 0, frame, NonceByteLength + TagByteLength, ciphertext.Length);

        CryptographicOperations.ZeroMemory(key);

        return new XElement(EncryptedKeyElementName, Convert.ToBase64String(frame));
    }

    internal static XElement Decrypt(string masterSecret, XElement encryptedElement)
    {
        ArgumentException.ThrowIfNullOrEmpty(masterSecret);
        ArgumentNullException.ThrowIfNull(encryptedElement);

        var frame = Convert.FromBase64String(encryptedElement.Value);
        if (frame.Length < NonceByteLength + TagByteLength)
        {
            throw new CryptographicException("The encrypted key-ring element is malformed (too short).");
        }

        var nonce = new byte[NonceByteLength];
        var tag = new byte[TagByteLength];
        var ciphertext = new byte[frame.Length - NonceByteLength - TagByteLength];
        Buffer.BlockCopy(frame, 0, nonce, 0, NonceByteLength);
        Buffer.BlockCopy(frame, NonceByteLength, tag, 0, TagByteLength);
        Buffer.BlockCopy(frame, NonceByteLength + TagByteLength, ciphertext, 0, ciphertext.Length);

        var key = DeriveKey(masterSecret);
        var plaintext = new byte[ciphertext.Length];
        try
        {
            using var aesGcm = new AesGcm(key, TagByteLength);
            aesGcm.Decrypt(nonce, ciphertext, tag, plaintext);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }

        return XElement.Parse(System.Text.Encoding.UTF8.GetString(plaintext));
    }

    private static byte[] DeriveKey(string masterSecret) =>
        HKDF.DeriveKey(
            HashAlgorithmName.SHA256,
            ikm: System.Text.Encoding.UTF8.GetBytes(masterSecret),
            outputLength: KeyByteLength,
            salt: null,
            info: System.Text.Encoding.UTF8.GetBytes(InfoLabel));
}
