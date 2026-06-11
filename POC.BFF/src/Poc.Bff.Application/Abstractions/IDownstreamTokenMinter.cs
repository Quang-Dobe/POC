namespace Poc.Bff.Application.Abstractions;

using Poc.Bff.Domain.Tokens;

public interface IDownstreamTokenMinter
{
    string Mint(DownstreamClaims claims);
}
