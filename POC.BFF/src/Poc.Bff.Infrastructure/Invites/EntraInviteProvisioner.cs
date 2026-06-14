namespace Poc.Bff.Infrastructure.Invites;

using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Poc.Bff.Application.Abstractions;
using Poc.Bff.Application.Configuration;
using Poc.Bff.Domain.Invites;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// PROD invite path: sends an Azure Entra B2B guest invitation via Microsoft Graph
/// (<c>POST /invitations</c>, <c>sendInvitationMessage:true</c>) so the invited user receives
/// an email containing a single-use redeem URL. No password is ever generated or returned.
/// </summary>
public sealed class EntraInviteProvisioner : IInviteProvisioner
{
    public const string HttpClientName = "EntraGraph";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<EntraInviteProvisioner> _logger;
    private readonly EntraGraphOptions _graph;
    private readonly string _frontendReturnUrl;

    public EntraInviteProvisioner(
        IHttpClientFactory httpClientFactory,
        IOptions<EntraGraphOptions> graphOptions,
        IOptions<AuthOptions> authOptions,
        ILogger<EntraInviteProvisioner> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(graphOptions);
        ArgumentNullException.ThrowIfNull(authOptions);
        ArgumentNullException.ThrowIfNull(logger);

        _httpClientFactory = httpClientFactory;
        _graph = graphOptions.Value;
        _frontendReturnUrl = authOptions.Value.FrontendReturnUrl;
        _logger = logger;
    }

    public async Task<InviteOutcome> ProvisionAsync(InviteRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Username);

        if (string.IsNullOrWhiteSpace(_graph.ClientSecret))
        {
            throw new InviteException(
                "The Entra Graph credential is unconfigured (EntraGraph:ClientSecret); " +
                "cannot provision an invited user.");
        }

        var graphToken = await GetGraphTokenAsync(ct).ConfigureAwait(false);
        return await SendInvitationAsync(graphToken, request, ct).ConfigureAwait(false);
    }

    private async Task<string> GetGraphTokenAsync(CancellationToken ct)
    {
        var tokenUrl = $"{_graph.Authority.TrimEnd('/')}/{_graph.TenantId}/oauth2/v2.0/token";
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = _graph.ClientId,
            ["client_secret"] = _graph.ClientSecret!,
            ["scope"] = _graph.Scope,
        };

        var client = _httpClientFactory.CreateClient(HttpClientName);
        using var content = new FormUrlEncodedContent(form);

        HttpResponseMessage response;
        try
        {
            response = await client.PostAsync(tokenUrl, content, ct).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            throw new InviteException("The Entra token endpoint is unreachable.", ex);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw new InviteException(
                    $"The Entra token request failed with status {(int)response.StatusCode}.");
            }

            var payload = await response.Content.ReadFromJsonAsync<TokenResponse>(ct).ConfigureAwait(false);
            if (payload is null || string.IsNullOrWhiteSpace(payload.AccessToken))
            {
                throw new InviteException("The Entra token response carried no access_token.");
            }

            return payload.AccessToken;
        }
    }

    private async Task<InviteOutcome> SendInvitationAsync(
        string graphToken,
        InviteRequest request,
        CancellationToken ct)
    {
        var invitationsUrl = $"{_graph.BaseUrl.TrimEnd('/')}/invitations";

        var body = new GraphInvitation(
            InvitedUserEmailAddress: request.Username,
            InviteRedirectUrl: _frontendReturnUrl,
            SendInvitationMessage: true);

        var client = _httpClientFactory.CreateClient(HttpClientName);
        using var message = new HttpRequestMessage(HttpMethod.Post, invitationsUrl)
        {
            Content = JsonContent.Create(body),
        };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", graphToken);

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(message, ct).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            throw new InviteException("The Entra Graph invitations endpoint is unreachable.", ex);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw new InviteException(
                    $"The Entra Graph invitation request failed with status {(int)response.StatusCode}.");
            }

            _logger.LogInformation("Entra invite: sent a B2B guest invitation email (single-use redeem URL).");
            return new InviteOutcome(request.Username, AlreadyExisted: false, InvitationSent: true);
        }
    }

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string? AccessToken);

    private sealed record GraphInvitation(
        [property: JsonPropertyName("invitedUserEmailAddress")] string InvitedUserEmailAddress,
        [property: JsonPropertyName("inviteRedirectUrl")] string InviteRedirectUrl,
        [property: JsonPropertyName("sendInvitationMessage")] bool SendInvitationMessage);
}
