using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Poc.Bff.Application.Abstractions;
using Poc.Bff.Application.Configuration;
using Poc.Bff.Domain.Invites;
using Poc.Bff.Domain.Rbac;
using Poc.Bff.Infrastructure.Agents;
using Poc.Bff.Infrastructure.Configuration;
using BffDataProtectionOptions = Poc.Bff.Infrastructure.Configuration.DataProtectionOptions;
using Poc.Bff.Infrastructure.Invites;
using Poc.Bff.Infrastructure.Oidc;
using Poc.Bff.Infrastructure.Rbac;
using Poc.Bff.Infrastructure.Secrets;
using Poc.Bff.Infrastructure.Tokens;
using StackExchange.Redis;

namespace Poc.Bff.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        string env)
    {
        services.AddSingleton<ISigningKeyProvider, SigningKeyProvider>();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IDownstreamTokenMinter, DownstreamTokenMinter>();

        services.AddHttpClient<IAgentGatewayClient, AgentGatewayClient>((sp, http) =>
        {
            var opts = sp.GetRequiredService<IOptions<AgentOptions>>().Value;
            http.BaseAddress = new Uri($"http://{opts.Host}:{opts.Port}");
            http.Timeout = Timeout.InfiniteTimeSpan;
        });

        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var sessionOptions = sp.GetRequiredService<IOptions<SessionOptions>>().Value;
            var redisConfig = ConfigurationOptions.Parse(sessionOptions.RedisAddress);
            return ConnectionMultiplexer.Connect(redisConfig);
        });

        var dpSection = configuration.GetSection(BffDataProtectionOptions.SectionName);
        var dpOptions = dpSection.Get<BffDataProtectionOptions>();
        var dpMasterSecret = configuration[SecretStoreConfiguration.DataProtectionMasterKeyKey];

        var dataProtection = services.AddDataProtection();
        if (dpOptions is not null)
        {
            dataProtection.SetApplicationName(dpOptions.ApplicationName);
        }

        if (dpOptions is not null && !string.IsNullOrEmpty(dpMasterSecret))
        {
            services.AddSingleton<IConfigureOptions<KeyManagementOptions>>(sp =>
                new ConfigureOptions<KeyManagementOptions>(options =>
                {
                    options.XmlEncryptor = new DataProtection.MasterKeyXmlEncryptor(dpMasterSecret);
                    options.XmlRepository =
                        new Microsoft.AspNetCore.DataProtection.StackExchangeRedis.RedisXmlRepository(
                            () => sp.GetRequiredService<IConnectionMultiplexer>().GetDatabase(),
                            dpOptions.RedisKey);
                }));
        }

        services.AddSingleton<ConfigRoleResolver>();
        services.AddSingleton<IRoleResolver, InviteAwareRoleResolver>();

        services.AddSingleton<IInviteStore, InMemoryInviteStore>();
        if (string.Equals(env, "PROD", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IInviteProvisioner, EntraInviteProvisioner>();

            services.AddHttpClient(EntraInviteProvisioner.HttpClientName);
        }
        else
        {
            services.AddSingleton<IInviteProvisioner, KeycloakInviteProvisioner>();

            services.AddHttpClient(KeycloakInviteProvisioner.HttpClientName)
                .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback =
                        HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
                });
        }

        services.AddSingleton<OidcCorrelationCookie>();

        services.AddHttpClient(OidcDiscoveryProvider.HttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = string.Equals(env, "DEV", StringComparison.OrdinalIgnoreCase)
                    ? HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                    : null,
            });

        services.AddSingleton<IOidcDiscoveryProvider, OidcDiscoveryProvider>();
        services.AddSingleton<IOidcAuthClient, OidcAuthClient>();

        return services;
    }
}
