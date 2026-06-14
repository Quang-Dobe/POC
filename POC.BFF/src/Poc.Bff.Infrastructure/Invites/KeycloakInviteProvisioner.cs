namespace Poc.Bff.Infrastructure.Invites;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Poc.Bff.Application.Configuration;
using Poc.Bff.Application.Abstractions;
using Poc.Bff.Domain.Invites;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// DEV invite path: provisions a Keycloak user with no credential, then triggers the realm's
/// native <c>execute-actions-email</c> so Keycloak emails the invited user a single-use action
/// link to set their own password (and verify email). No password is ever generated or returned.
/// </summary>
public sealed class KeycloakInviteProvisioner : IInviteProvisioner
{
    public const string HttpClientName = "KeycloakAdmin";

    private static readonly string[] EmailActions = { "UPDATE_PASSWORD", "VERIFY_EMAIL" };

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

        var baseUrl = _admin.AdminBaseUrl.TrimEnd('/');
        var adminToken = await GetAdminTokenAsync(baseUrl, ct).ConfigureAwait(false);

        var userId = await CreateUserAsync(baseUrl, adminToken, request, ct).ConfigureAwait(false);
        if (userId is null)
        {
            _logger.LogInformation("Keycloak invite: the user already existed (idempotent re-invite).");
            return new InviteOutcome(request.Username, AlreadyExisted: true, InvitationSent: false);
        }

        await SendActionsEmailAsync(baseUrl, adminToken, userId, ct).ConfigureAwait(false);

        _logger.LogInformation("Keycloak invite: provisioned a new user and sent the action-email link.");
        return new InviteOutcome(request.Username, AlreadyExisted: false, InvitationSent: true);
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

    /// <summary>
    /// Creates the user with no credential. Returns the new user's id, or <c>null</c> when the
    /// user already existed (HTTP 409, idempotent re-invite).
    /// </summary>
    private async Task<string?> CreateUserAsync(
        string baseUrl,
        string adminToken,
        InviteRequest request,
        CancellationToken ct)
    {
        var usersUrl = $"{baseUrl}/admin/realms/{_admin.Realm}/users";

        var body = new CreateUserRequest(
            Username: request.Username,
            Enabled: true,
            Email: request.DisplayName ?? request.Username,
            Attributes: new Dictionary<string, string[]>
            {
                ["tenantId"] = new[] { _invite.TenantId },
                ["region"] = new[] { _invite.DefaultRegion ?? string.Empty },
            },
            RequiredActions: EmailActions);

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
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new InviteException(
                    $"The Keycloak create-user request failed with status {(int)response.StatusCode}.");
            }

            var location = response.Headers.Location?.ToString();
            if (string.IsNullOrWhiteSpace(location))
            {
                throw new InviteException(
                    "The Keycloak create-user response carried no Location header; cannot resolve the user id.");
            }

            return location.TrimEnd('/').Split('/')[^1];
        }
    }

    private async Task SendActionsEmailAsync(
        string baseUrl,
        string adminToken,
        string userId,
        CancellationToken ct)
    {
        var emailUrl = $"{baseUrl}/admin/realms/{_admin.Realm}/users/{userId}/execute-actions-email";

        var query = new Dictionary<string, string?>();
        if (!string.IsNullOrWhiteSpace(_admin.RedeemClientId))
        {
            query["client_id"] = _admin.RedeemClientId;
        }
        if (!string.IsNullOrWhiteSpace(_admin.RedeemRedirectUri))
        {
            query["redirect_uri"] = _admin.RedeemRedirectUri;
        }
        if (query.Count > 0)
        {
            emailUrl = QueryHelpers.AddQueryString(emailUrl, query);
        }

        var client = _httpClientFactory.CreateClient(HttpClientName);
        using var message = new HttpRequestMessage(HttpMethod.Put, emailUrl)
        {
            Content = JsonContent.Create(EmailActions),
        };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(message, ct).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            throw new InviteException("The Keycloak execute-actions-email endpoint is unreachable.", ex);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw new InviteException(
                    $"The Keycloak execute-actions-email request failed with status {(int)response.StatusCode}.");
            }
        }
    }

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string? AccessToken);

    private sealed record CreateUserRequest(
        [property: JsonPropertyName("username")] string Username,
        [property: JsonPropertyName("enabled")] bool Enabled,
        [property: JsonPropertyName("email")] string? Email,
        [property: JsonPropertyName("attributes")] IReadOnlyDictionary<string, string[]> Attributes,
        [property: JsonPropertyName("requiredActions")] IReadOnlyList<string> RequiredActions);
}
