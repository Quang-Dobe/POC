namespace Api.Secrets;

internal static class SecretKeyMapping
{

    internal static string ToConfigKey(string storeName) => storeName.Replace("--", ":");
}
