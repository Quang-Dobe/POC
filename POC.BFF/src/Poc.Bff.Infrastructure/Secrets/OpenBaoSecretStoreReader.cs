namespace Poc.Bff.Infrastructure.Secrets;

using VaultSharp;
using VaultSharp.V1.AuthMethods.Token;
using VaultSharp.V1.Commons;
using Poc.Bff.Application.Abstractions;

internal sealed class OpenBaoSecretStoreReader : ISecretStoreReader
{
    private const string MountPoint = "secret";
    private const string SecretPath = "poc";
    private const string BaoTokenEnvVar = "BAO_TOKEN";

    private readonly string _address;

    internal OpenBaoSecretStoreReader(string address)
    {
        _address = address;
    }

    public IReadOnlyDictionary<string, string?> Load()
    {
        var token = Environment.GetEnvironmentVariable(BaoTokenEnvVar);
        if (string.IsNullOrWhiteSpace(token))
        {

            throw new SecretStoreException(
                $"OpenBAO secret store ({_address}): required bootstrap token env var " +
                $"'{BaoTokenEnvVar}' is not set. Cannot authenticate to the DEV secret store.");
        }

        Secret<SecretData> read;
        try
        {
            var authMethod = new TokenAuthMethodInfo(token);
            var settings = new VaultClientSettings(_address, authMethod);
            var client = new VaultClient(settings);

            read = client.V1.Secrets.KeyValue.V2
                .ReadSecretAsync(path: SecretPath, mountPoint: MountPoint)
                .GetAwaiter().GetResult();
        }
        catch (SecretStoreException)
        {
            throw;
        }
        catch (Exception ex)
        {

            throw new SecretStoreException(
                $"OpenBAO secret store ({_address}, {MountPoint}/{SecretPath}) is unreachable " +
                $"or rejected the request: {ex.GetType().Name}.", ex);
        }

        var data = read.Data?.Data;
        if (data is null)
        {
            throw new SecretStoreException(
                $"OpenBAO secret store ({_address}, {MountPoint}/{SecretPath}) returned no data.");
        }

        var result = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var (rawKey, rawValue) in data)
        {

            var configKey = SecretKeyMapping.ToConfigKey(rawKey);
            result[configKey] = rawValue?.ToString();
        }

        return result;
    }
}
