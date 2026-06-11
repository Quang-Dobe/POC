namespace Poc.Bff.Infrastructure.Oidc;

using Poc.Bff.Application.Abstractions;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.Tokens;

public sealed class OidcCorrelationCookie
{
    public const string Name = "poc.oidc";

    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    public void Append(HttpResponse response, OidcAuthCorrelation correlation, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(correlation);

        var payload = Base64UrlEncoder.Encode(JsonSerializer.SerializeToUtf8Bytes(correlation));
        response.Cookies.Append(Name, payload, BuildOptions(now));
    }

    public OidcAuthCorrelation? Read(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!request.Cookies.TryGetValue(Name, out var value) || string.IsNullOrEmpty(value))
        {
            return null;
        }

        try
        {
            var bytes = Base64UrlEncoder.DecodeBytes(value);
            return JsonSerializer.Deserialize<OidcAuthCorrelation>(bytes);
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            return null;
        }
    }

    public void Delete(HttpResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        response.Cookies.Delete(Name, BuildDeleteOptions());
    }

    private static CookieOptions BuildOptions(DateTimeOffset now) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Lax,
        Path = "/",
        Expires = now.Add(Lifetime),
    };

    private static CookieOptions BuildDeleteOptions() => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Lax,
        Path = "/",
    };
}
