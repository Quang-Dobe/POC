namespace Poc.Bff.Application.Abstractions;

using Microsoft.IdentityModel.Tokens;

public interface ISigningKeyProvider
{
    SigningCredentials CurrentSigningCredentials();

    JwksDocument PublicJwks();
}
