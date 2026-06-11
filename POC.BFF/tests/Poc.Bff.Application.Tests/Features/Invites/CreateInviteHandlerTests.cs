using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Poc.Bff.Application.Abstractions;
using Poc.Bff.Application.Common.Results;
using Poc.Bff.Application.Configuration;
using Poc.Bff.Application.Features.Invites;
using Poc.Bff.Application.Features.Invites.CreateInvite;
using Poc.Bff.Domain.Invites;
using Xunit;

namespace Poc.Bff.Application.Tests.Features.Invites;

public class CreateInviteHandlerTests
{
    private static readonly string[] DefaultRoles = { "reader" };
    private const string DefaultRegion = "Manhattan County";

    private readonly IInviteProvisioner _provisioner = Substitute.For<IInviteProvisioner>();
    private readonly IInviteStore _store = Substitute.For<IInviteStore>();
    private readonly IOptions<InviteOptions> _options;
    private readonly CreateInviteHandler _handler;

    public CreateInviteHandlerTests()
    {
        _options = Options.Create(new InviteOptions
        {
            DefaultRoles = DefaultRoles,
            DefaultRegion = DefaultRegion,
            TenantId = "test-tenant",
        });

        _handler = new CreateInviteHandler(_provisioner, _store, _options);
    }

    [Fact]
    public async Task Handle_ProvisionerSucceeds_ReturnsSuccessWithMappedResponse()
    {
        var outcome = new InviteOutcome("erin@example.com", "https://invite.stub/redeem/x", AlreadyExisted: false);
        _provisioner
            .ProvisionAsync(Arg.Any<InviteRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(outcome));

        var command = new CreateInviteCommand(Username: "erin@example.com");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Subject.Should().Be("erin@example.com");
        result.Value.RedeemUrl.Should().Be("https://invite.stub/redeem/x");
        result.Value.AlreadyExisted.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ProvisionerSucceeds_CallsStoreAddWithGrantFromOptions()
    {

        var outcome = new InviteOutcome("erin@example.com", "https://invite.stub/redeem/x", AlreadyExisted: false);
        _provisioner
            .ProvisionAsync(Arg.Any<InviteRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(outcome));

        var command = new CreateInviteCommand(Username: "erin@example.com");

        await _handler.Handle(command, CancellationToken.None);

        _store.Received(1).Add(
            "erin@example.com",
            Arg.Is<InviteGrant>(g =>
                g.Region == DefaultRegion &&
                g.Roles.SequenceEqual(DefaultRoles)));
    }

    [Fact]
    public async Task Handle_ProvisionerSucceeds_PassesCommandFieldsToProvisioner()
    {

        InviteRequest? captured = null;
        var outcome = new InviteOutcome("erin@example.com", "https://invite.stub/redeem/x", AlreadyExisted: false);
        _provisioner
            .ProvisionAsync(
                Arg.Do<InviteRequest>(r => captured = r),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(outcome));

        var command = new CreateInviteCommand(Username: "erin@example.com", DisplayName: "Erin Example");

        await _handler.Handle(command, CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.Username.Should().Be("erin@example.com");
        captured.DisplayName.Should().Be("Erin Example");
    }

    [Fact]
    public async Task Handle_ProvisionerThrowsInviteException_ReturnsFailureWithUpstreamError()
    {

        _provisioner
            .ProvisionAsync(Arg.Any<InviteRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InviteException("Upstream IdP rejected the invite."));

        var command = new CreateInviteCommand(Username: "erin@example.com");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(InviteErrors.ProvisioningFailed.Code);
        result.Error.Type.Should().Be(ErrorType.Upstream);
    }

    [Fact]
    public async Task Handle_ProvisionerThrowsInviteException_DoesNotCallStore()
    {

        _provisioner
            .ProvisionAsync(Arg.Any<InviteRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InviteException("Upstream IdP rejected the invite."));

        var command = new CreateInviteCommand(Username: "erin@example.com");

        await _handler.Handle(command, CancellationToken.None);

        _store.DidNotReceive().Add(Arg.Any<string>(), Arg.Any<InviteGrant>());
    }
}
