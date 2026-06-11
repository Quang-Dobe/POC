namespace Poc.Bff.Application.Abstractions;

public interface ISecretStoreReader
{

    IReadOnlyDictionary<string, string?> Load();
}
