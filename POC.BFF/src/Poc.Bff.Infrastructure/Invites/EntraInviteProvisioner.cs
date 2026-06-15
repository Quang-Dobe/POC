namespace Poc.Bff.Infrastructure.Invites;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Poc.Bff.Application.Abstractions;
using Poc.Bff.Application.Configuration;
using Poc.Bff.Domain.Invites;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// PROD invite path: creates a member user in Azure Entra via Microsoft Graph (<c>POST /users</c>)
/// with the fixed default password sourced from the secret store (<c>Invite:DefaultPassword</c>),
/// using a client-credentials Graph token. The invited user signs in with that password.
/// </summary>
public sealed class EntraInviteProvisioner : IInviteProvisioner
{
    public const string HttpClientName = "EntraGraph";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<EntraInviteProvisioner> _logger;
    private readonly EntraGraphOptions _graph;
    private readonly InviteOptions _invite;

    public EntraInviteProvisioner(
        IHttpClientFactory httpClientFactory,
        IOptions<EntraGraphOptions> graphOptions,
        IOptions<InviteOptions> inviteOptions,
        ILogger<EntraInviteProvisioner> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(graphOptions);
        ArgumentNullException.ThrowIfNull(inviteOptions);
        ArgumentNullException.ThrowIfNull(logger);

        _httpClientFactory = httpClientFactory;
        _graph = graphOptions.Value;
        _invite = inviteOptions.Value;
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

        if (string.IsNullOrWhiteSpace(_invite.DefaultPassword))
        {
            throw new InviteException(
                "The invite default password is unconfigured (Invite:DefaultPassword in the secret " +
                "store); cannot provision an invited user.");
        }

        var graphToken = await GetGraphTokenAsync(ct).ConfigureAwait(false);
        return await CreateUserAsync(graphToken, request, ct).ConfigureAwait(false);
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

    private async Task<InviteOutcome> CreateUserAsync(
        string graphToken,
        InviteRequest request,
        CancellationToken ct)
    {
        var usersUrl = $"{_graph.BaseUrl.TrimEnd('/')}/users";

        var mailNickname = MailNickname(request.Username);
        var userPrincipalName = $"{mailNickname}@{_graph.UserDomain}";
        var body = new CreateUserRequest(
            AccountEnabled: true,
            DisplayName: request.DisplayName ?? request.Username,
            MailNickname: mailNickname,
            UserPrincipalName: userPrincipalName,
            Mail: request.Username,
            PasswordProfile: new PasswordProfile(
                Password: _invite.DefaultPassword!,
                ForceChangePasswordNextSignIn: false));

        var client = _httpClientFactory.CreateClient(HttpClientName);
        using var message = new HttpRequestMessage(HttpMethod.Post, usersUrl)
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
            throw new InviteException("The Entra Graph users endpoint is unreachable.", ex);
        }

        using (response)
        {
            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                _logger.LogInformation("Entra invite: the user already existed (idempotent re-invite).");
                return new InviteOutcome(userPrincipalName, AlreadyExisted: true, PasswordSet: false);
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new InviteException(
                    $"The Entra Graph create-user request failed with status {(int)response.StatusCode}.");
            }

            _logger.LogInformation("Entra invite: provisioned a new member user with the default password.");
            return new InviteOutcome(userPrincipalName, AlreadyExisted: false, PasswordSet: true);
        }
    }

    private static string MailNickname(string email)
    {
        var at = email.IndexOf('@', StringComparison.Ordinal);
        return at > 0 ? email[..at] : email;
    }

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string? AccessToken);

    private sealed record CreateUserRequest(
        [property: JsonPropertyName("accountEnabled")] bool AccountEnabled,
        [property: JsonPropertyName("displayName")] string DisplayName,
        [property: JsonPropertyName("mailNickname")] string MailNickname,
        [property: JsonPropertyName("userPrincipalName")] string UserPrincipalName,
        [property: JsonPropertyName("mail")] string Mail,
        [property: JsonPropertyName("passwordProfile")] PasswordProfile PasswordProfile);

    private sealed record PasswordProfile(
        [property: JsonPropertyName("password")] string Password,
        [property: JsonPropertyName("forceChangePasswordNextSignIn")] bool ForceChangePasswordNextSignIn);
}
