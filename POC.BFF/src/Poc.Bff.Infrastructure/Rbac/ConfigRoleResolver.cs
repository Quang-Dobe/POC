namespace Poc.Bff.Infrastructure.Rbac;

using Poc.Bff.Application.Abstractions;
using Poc.Bff.Domain.Rbac;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public sealed class ConfigRoleResolver : IRoleResolver
{
    private readonly RoleMap _roleMap;
    private readonly ILogger<ConfigRoleResolver> _logger;

    public ConfigRoleResolver(IOptions<RoleMap> options, ILogger<ConfigRoleResolver> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _roleMap = options.Value;
        _logger = logger;
    }

    public RoleResolution Resolve(string subject)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);

        if (_roleMap.Entries.TryGetValue(subject, out var entry))
        {
            if (entry.Roles is { Length: > 0 } && !string.IsNullOrWhiteSpace(entry.Region))
            {
                _logger.LogInformation("Resolved a known subject to its mapped roles + region.");
                return RoleResolution.Allowed(entry.Roles, entry.Region);
            }

            _logger.LogWarning(
                "A RoleMap entry exists for the subject but is missing roles or region; denying.");
            return RoleResolution.Denied;
        }

        if (_roleMap.HasDefault)
        {
            _logger.LogInformation("Resolved an unknown subject to the configured default-reader.");
            return RoleResolution.Allowed(_roleMap.DefaultRoles!, _roleMap.DefaultRegion!);
        }

        _logger.LogWarning("Denied a login: the subject is unknown and no default-reader is configured.");
        return RoleResolution.Denied;
    }
}
