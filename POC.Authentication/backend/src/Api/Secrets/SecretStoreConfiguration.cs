namespace Api.Secrets;

using Microsoft.Extensions.Configuration;

public static class SecretStoreConfiguration
{
    internal const string ProdEnv = "PROD";

    internal const string DisplayStringKey = "Message:DisplayString";

    private static readonly string[] RequiredKeys = { DisplayStringKey };

    internal static Func<string, IConfiguration, ISecretStoreReader>? ReaderFactoryOverride;

    public static IConfigurationBuilder AddEnvSelectedSecretStore(
        this IConfigurationBuilder builder,
        string env,
        IConfiguration config)
        => builder.AddEnvSelectedSecretStore(
            env,
            _ => (ReaderFactoryOverride ?? CreateReader)(env, config));

    internal static IConfigurationBuilder AddEnvSelectedSecretStore(
        this IConfigurationBuilder builder,
        string env,
        Func<string, ISecretStoreReader> readerFactory)
    {
        var reader = readerFactory(env);

        var secrets = reader.Load();

        foreach (var key in RequiredKeys)
        {
            if (!secrets.TryGetValue(key, out var value) || string.IsNullOrEmpty(value))
            {
                throw new SecretStoreException(
                    $"{StoreName(env)} did not supply the required secret '{key}'. " +
                    "Refusing to start without it.");
            }
        }

        builder.AddInMemoryCollection(secrets);
        return builder;
    }

    private static ISecretStoreReader CreateReader(string env, IConfiguration config)
    {
        if (IsProd(env))
        {
            var vaultUri = config["KeyVault:VaultUri"];
            if (string.IsNullOrWhiteSpace(vaultUri) || !Uri.TryCreate(vaultUri, UriKind.Absolute, out var uri))
            {
                throw new SecretStoreException(
                    "Azure Key Vault selected (ENV=PROD) but 'KeyVault:VaultUri' is missing or not " +
                    "an absolute URI in configuration. Cannot resolve the secret store.");
            }

            return new KeyVaultSecretStoreReader(uri);
        }

        var address = config["Vault:Address"];
        if (string.IsNullOrWhiteSpace(address))
        {
            throw new SecretStoreException(
                "OpenBAO selected (ENV=DEV) but 'Vault:Address' is missing in configuration. " +
                "Cannot resolve the secret store.");
        }

        return new OpenBaoSecretStoreReader(address);
    }

    private static bool IsProd(string env) =>
        string.Equals(env, ProdEnv, StringComparison.OrdinalIgnoreCase);

    private static string StoreName(string env) =>
        IsProd(env) ? "Azure Key Vault (PROD)" : "OpenBAO secret store (DEV)";
}
