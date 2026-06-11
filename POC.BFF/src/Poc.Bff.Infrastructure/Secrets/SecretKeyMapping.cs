namespace Poc.Bff.Infrastructure.Secrets;

internal static class SecretKeyMapping
{

    internal static string ToConfigKey(string storeName) => storeName.Replace("--", ":");
}
