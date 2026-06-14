namespace Poc.Bff.Infrastructure.Invites;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Poc.Bff.Application.Configuration;
using Poc.Bff.Application.Abstractions;
using Poc.Bff.Domain.Invites;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// DEV invite path: provisions a Keycloak user with the fixed default password sourced from the
/// secret store (<c>Invite:DefaultPassword</c>). The invited user signs in with that password.
/// </summary>
public sealed class KeycloakInviteProvisioner : IInviteProvisioner
{
    public const string HttpClientName = "KeycloakAdmin";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<KeycloakInviteProvisioner> _logger;
    private readonly KeycloakAdminOptions _admin;
    private readonly InviteOptions _invite;

    public KeycloakInviteProvisioner(
        IHttpClientFactory httpClientFactory,
        IOptions<KeycloakAdminOptions> adminOptions,
        IOptions<InviteOptions> inviteOptions,
        ILogger<KeycloakInviteProvisioner> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(adminOptions);
        ArgumentNullException.ThrowIfNull(inviteOptions);
        ArgumentNullException.ThrowIfNull(logger);

        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _admin = adminOptions.Value;
        _invite = inviteOptions.Value;
    }

    public async Task<InviteOutcome> ProvisionAsync(InviteRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Username);

        if (string.IsNullOrWhiteSpace(_admin.AdminClientSecret))
        {
            throw new InviteException(
                "The Keycloak admin credential is unconfigured (Keycloak:AdminClientSecret); " +
                "cannot provision an invited user.");
        }

        if (string.IsNullOrWhiteSpace(_invite.DefaultPassword))
        {
            throw new InviteException(
                "The invite default password is unconfigured (Invite:DefaultPassword in the secret " +
                "store); cannot provision an invited user.");
        }

        var baseUrl = _admin.AdminBaseUrl.TrimEnd('/');
        var adminToken = await GetAdminTokenAsync(baseUrl, ct).ConfigureAwait(false);
        return await CreateUserAsync(baseUrl, adminToken, request, ct).ConfigureAwait(false);
    }

    private async Task<string> GetAdminTokenAsync(string baseUrl, CancellationToken ct)
    {
        var tokenUrl = $"{baseUrl}/realms/{_admin.Realm}/protocol/openid-connect/token";
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = _admin.AdminClientId,
            ["client_secret"] = _admin.AdminClientSecret!,
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
            throw new InviteException("The Keycloak admin token endpoint is unreachable.", ex);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw new InviteException(
                    $"The Keycloak admin token request failed with status {(int)response.StatusCode}.");
            }

            var payload = await response.Content.ReadFromJsonAsync<TokenResponse>(ct).ConfigureAwait(false);
            if (payload is null || string.IsNullOrWhiteSpace(payload.AccessToken))
            {
                throw new InviteException("The Keycloak admin token response carried no access_token.");
            }

            return payload.AccessToken;
        }
    }

    private async Task<InviteOutcome> CreateUserAsync(
        string baseUrl,
        string adminToken,
        InviteRequest request,
        CancellationToken ct)
    {
        var usersUrl = $"{baseUrl}/admin/realms/{_admin.Realm}/users";

        var body = new CreateUserRequest(
            Username: request.Username,
            Enabled: true,
            EmailVerified: true,
            Email: request.DisplayName ?? request.Username,
            Attributes: new Dictionary<string, string[]>
            {
                ["tenantId"] = new[] { _invite.TenantId },
                ["region"] = new[] { _invite.DefaultRegion ?? string.Empty },
            },
            Credentials: new[]
            {
                new CredentialRequest(Type: "password", Value: _invite.DefaultPassword!, Temporary: false),
            });

        var client = _httpClientFactory.CreateClient(HttpClientName);
        using var message = new HttpRequestMessage(HttpMethod.Post, usersUrl)
        {
            Content = JsonContent.Create(body),
        };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(message, ct).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            throw new InviteException("The Keycloak admin users endpoint is unreachable.", ex);
        }

        using (response)
        {
            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                _logger.LogInformation("Keycloak invite: the user already existed (idempotent re-invite).");
                return new InviteOutcome(request.Username, AlreadyExisted: true, PasswordSet: false);
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new InviteException(
                    $"The Keycloak create-user request failed with status {(int)response.StatusCode}.");
            }

            _logger.LogInformation("Keycloak invite: provisioned a new user with the default password.");
            return new InviteOutcome(request.Username, AlreadyExisted: false, PasswordSet: true);
        }
    }

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string? AccessToken);

    private sealed record CreateUserRequest(
        [property: JsonPropertyName("username")] string Username,
        [property: JsonPropertyName("enabled")] bool Enabled,
        [property: JsonPropertyName("emailVerified")] bool EmailVerified,
        [property: JsonPropertyName("email")] string? Email,
        [property: JsonPropertyName("attributes")] IReadOnlyDictionary<string, string[]> Attributes,
        [property: JsonPropertyName("credentials")] IReadOnlyList<CredentialRequest> Credentials);

    private sealed record CredentialRequest(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("value")] string Value,
        [property: JsonPropertyName("temporary")] bool Temporary);
}
