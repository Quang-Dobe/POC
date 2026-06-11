namespace Poc.Bff.Infrastructure.Tests.Invites;

using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Poc.Bff.Application.Configuration;
using Poc.Bff.Domain.Invites;
using Poc.Bff.Infrastructure.Invites;
using Xunit;

public class EntraInviteProvisionerTests
{
    private static EntraInviteProvisioner Build()
    {
        var authOptions = Options.Create(new AuthOptions
        {
            Authority = "https://login.microsoftonline.com/tenant/v2.0",
            Audience = "api://x",
            ClientId = "bff",
            RedirectUri = "https://bff/auth/callback",
            FrontendReturnUrl = "https://spa.example",
        });
        return new EntraInviteProvisioner(authOptions, NullLogger<EntraInviteProvisioner>.Instance);
    }

    [Fact]
    public async Task ProvisionAsync_ReturnsDeterministicOutcome_WithoutAzure()
    {
        var provisioner = Build();

        var outcome = await provisioner.ProvisionAsync(new InviteRequest("guest@x.com"));

        Assert.Equal("guest@x.com", outcome.Subject);
        Assert.StartsWith("https://invite.stub/redeem/", outcome.RedeemUrl);
        Assert.False(outcome.AlreadyExisted);
    }

    [Fact]
    public async Task ProvisionAsync_IsStable_AcrossCalls()
    {
        var provisioner = Build();

        var first = await provisioner.ProvisionAsync(new InviteRequest("guest@x.com"));
        var second = await provisioner.ProvisionAsync(new InviteRequest("GUEST@x.com"));

        Assert.Equal(first.RedeemUrl, second.RedeemUrl);
    }
}
