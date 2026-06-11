namespace Poc.Bff.Application.Abstractions;

using Poc.Bff.Domain.Rbac;

public interface IRoleResolver
{
    RoleResolution Resolve(string subject);
}
