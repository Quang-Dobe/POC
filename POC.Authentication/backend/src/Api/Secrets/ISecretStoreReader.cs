namespace Api.Secrets;

public interface ISecretStoreReader
{

    IReadOnlyDictionary<string, string?> Load();
}
