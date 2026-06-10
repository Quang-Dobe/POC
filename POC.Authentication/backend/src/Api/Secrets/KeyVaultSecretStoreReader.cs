namespace Api.Secrets;

using Azure;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;

internal sealed class KeyVaultSecretStoreReader : ISecretStoreReader
{
    private readonly Uri _vaultUri;

    internal KeyVaultSecretStoreReader(Uri vaultUri)
    {
        _vaultUri = vaultUri;
    }

    public IReadOnlyDictionary<string, string?> Load()
    {
        var result = new Dictionary<string, string?>(StringComparer.Ordinal);
        try
        {
            var client = new SecretClient(_vaultUri, new DefaultAzureCredential());

            foreach (var prop in client.GetPropertiesOfSecrets())
            {
                KeyVaultSecret secret = client.GetSecret(prop.Name);

                var configKey = SecretKeyMapping.ToConfigKey(secret.Name);
                result[configKey] = secret.Value;
            }
        }
        catch (Exception ex) when (ex is RequestFailedException or AuthenticationFailedException or CredentialUnavailableException)
        {

            throw new SecretStoreException(
                $"Azure Key Vault ({_vaultUri}) is unreachable or credential resolution failed: " +
                $"{ex.GetType().Name}.", ex);
        }

        return result;
    }
}
