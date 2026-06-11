namespace Poc.Bff.Infrastructure.Tests.Secrets;

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Poc.Bff.Application.Abstractions;
using Poc.Bff.Infrastructure.Secrets;
using Xunit;

public class SecretStoreSelectionTests
{
    private const string DisplayStringKey = "Message:DisplayString";

    private sealed class FakeReader : ISecretStoreReader
    {
        private readonly IReadOnlyDictionary<string, string?> _secrets;

        internal FakeReader(IReadOnlyDictionary<string, string?> secrets) => _secrets = secrets;

        public IReadOnlyDictionary<string, string?> Load() => _secrets;
    }

    private sealed class UnreachableReader : ISecretStoreReader
    {
        public IReadOnlyDictionary<string, string?> Load() =>
            throw new SecretStoreException("store unreachable (fake)");
    }

    private static IReadOnlyDictionary<string, string?> ValidSecrets() =>
        new Dictionary<string, string?>
        {
            [DisplayStringKey] = "fake-display-string",
            [SecretStoreConfiguration.SigningKeyPemKey] = Support.TestRsaKey.NewPrivatePem(),
            [SecretStoreConfiguration.DataProtectionMasterKeyKey] = "fake-master-secret",
        };

    [Fact]
    public void AddEnvSelectedSecretStore_EnvDev_SelectsOpenBaoBranch()
    {
        string? seenEnv = null;
        var builder = new ConfigurationBuilder();

        builder.AddEnvSelectedSecretStore("DEV", env =>
        {
            seenEnv = env;
            return new FakeReader(ValidSecrets());
        });

        Assert.Equal("DEV", seenEnv);
    }

    [Fact]
    public void AddEnvSelectedSecretStore_EnvProd_SelectsKeyVaultBranch()
    {
        string? seenEnv = null;
        var builder = new ConfigurationBuilder();

        builder.AddEnvSelectedSecretStore("PROD", env =>
        {
            seenEnv = env;
            return new FakeReader(ValidSecrets());
        });

        Assert.Equal("PROD", seenEnv);
    }

    [Theory]
    [InlineData("DEV")]
    [InlineData("PROD")]
    public void AddEnvSelectedSecretStore_BothModes_SurfaceSameLogicalKeys(string env)
    {
        var builder = new ConfigurationBuilder();

        builder.AddEnvSelectedSecretStore(env, _ => new FakeReader(ValidSecrets()));
        var config = builder.Build();

        Assert.Equal("fake-display-string", config[DisplayStringKey]);
    }

    [Theory]
    [InlineData("DEV")]
    [InlineData("PROD")]
    public void AddEnvSelectedSecretStore_StoreUnreachable_FailsFast(string env)
    {
        var builder = new ConfigurationBuilder();

        var ex = Assert.Throws<SecretStoreException>(
            () => builder.AddEnvSelectedSecretStore(env, _ => new UnreachableReader()));

        Assert.DoesNotContain("fake-display-string", ex.Message);
    }

    [Theory]
    [InlineData("DEV")]
    [InlineData("PROD")]
    public void AddEnvSelectedSecretStore_MissingExpectedSecret_FailsFast(string env)
    {
        var partial = new Dictionary<string, string?>();
        var builder = new ConfigurationBuilder();

        var ex = Assert.Throws<SecretStoreException>(
            () => builder.AddEnvSelectedSecretStore(env, _ => new FakeReader(partial)));

        Assert.Contains(DisplayStringKey, ex.Message);
    }

    [Theory]
    [InlineData("DEV")]
    [InlineData("PROD")]
    public void AddEnvSelectedSecretStore_EmptySecretValue_FailsFast(string env)
    {
        var secrets = new Dictionary<string, string?>
        {
            [DisplayStringKey] = string.Empty,
        };
        var builder = new ConfigurationBuilder();

        var ex = Assert.Throws<SecretStoreException>(
            () => builder.AddEnvSelectedSecretStore(env, _ => new FakeReader(secrets)));

        Assert.Contains(DisplayStringKey, ex.Message);
    }

    [Theory]
    [InlineData("Message--DisplayString", "Message:DisplayString")]
    [InlineData("NoSeparator", "NoSeparator")]
    public void SecretKeyMapping_MapsStoreNamesToConfigKeys(string storeName, string expected)
    {
        Assert.Equal(expected, SecretKeyMapping.ToConfigKey(storeName));
    }

    [Fact]
    public void OpenBaoReader_MissingBaoToken_FailsFastWithoutNetwork()
    {
        var original = Environment.GetEnvironmentVariable("BAO_TOKEN");
        try
        {
            Environment.SetEnvironmentVariable("BAO_TOKEN", null);
            var reader = new OpenBaoSecretStoreReader("http://localhost:8200");

            var ex = Assert.Throws<SecretStoreException>(() => reader.Load());

            Assert.Contains("BAO_TOKEN", ex.Message);
        }
        finally
        {
            Environment.SetEnvironmentVariable("BAO_TOKEN", original);
        }
    }
}
