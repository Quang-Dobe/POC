using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Poc.Bff.Application.Common;
using Poc.Bff.Application.Configuration;
using Poc.Bff.Application.Features.Message;
using Poc.Bff.Domain.Rbac;
using Poc.Bff.Infrastructure;
using Poc.Bff.Infrastructure.Configuration;
using Poc.Bff.Infrastructure.Cookies;
using Poc.Bff.Infrastructure.Rbac;
using Poc.Bff.Infrastructure.Secrets;
using Poc.Bff.Infrastructure.Session;
using BffSessionOptions = Poc.Bff.Infrastructure.Configuration.SessionOptions;
using BffCorsOptions = Poc.Bff.Infrastructure.Configuration.CorsOptions;
using BffDataProtectionOptions = Poc.Bff.Infrastructure.Configuration.DataProtectionOptions;

var builder = WebApplication.CreateBuilder(args);

var env = builder.Configuration["ENV"] ?? "DEV";

builder.Configuration.AddJsonFile($"appsettings.{env}.json", optional: true, reloadOnChange: false);
builder.Configuration.AddEnvironmentVariables();

var disableSecretStore = string.Equals(
    builder.Configuration["DisableSecretStore"], "true", StringComparison.OrdinalIgnoreCase);

if (!disableSecretStore)
{
    builder.Configuration.AddEnvSelectedSecretStore(env, builder.Configuration);
}

builder.Services.AddOptions<AuthOptions>()
    .Bind(builder.Configuration.GetSection(AuthOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<BffCorsOptions>()
    .Bind(builder.Configuration.GetSection(BffCorsOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<MessageOptions>()
    .Bind(builder.Configuration.GetSection(MessageOptions.SectionName));

builder.Services.AddOptions<BffSessionOptions>()
    .Bind(builder.Configuration.GetSection(BffSessionOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<IdpSimulatorOptions>()
    .Bind(builder.Configuration.GetSection(IdpSimulatorOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<BffDataProtectionOptions>()
    .Bind(builder.Configuration.GetSection(BffDataProtectionOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<RoleMap>()
    .Bind(builder.Configuration.GetSection(RoleMap.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<InviteOptions>()
    .Bind(builder.Configuration.GetSection(InviteOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<KeycloakAdminOptions>()
    .Bind(builder.Configuration.GetSection(KeycloakAdminOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<AgentOptions>()
    .Bind(builder.Configuration.GetSection(AgentOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration, env);

var sessionConfig = builder.Configuration.GetSection(BffSessionOptions.SectionName).Get<BffSessionOptions>()
    ?? throw new InvalidOperationException(
        $"The '{BffSessionOptions.SectionName}' configuration section is missing; cannot wire cookie auth.");

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = sessionConfig.CookieName;
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(sessionConfig.TtlMinutes);
        options.SlidingExpiration = false;
        options.Events = new SlidingCookieEvents(TimeProvider.System)
        {
            OnRedirectToLogin = ctx =>
            {
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            },
            OnRedirectToAccessDenied = ctx =>
            {
                ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            },
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(SessionDefaults.Policy, policy => policy
        .AddAuthenticationSchemes(CookieAuthenticationDefaults.AuthenticationScheme)
        .RequireAuthenticatedUser());

    options.AddPolicy(RbacPolicies.AskPolicy, policy => policy
        .AddAuthenticationSchemes(CookieAuthenticationDefaults.AuthenticationScheme)
        .RequireAuthenticatedUser()
        .RequireRole(RbacPolicies.ReaderRole, RbacPolicies.ManagerRole));

    options.AddPolicy(RbacPolicies.InvitePolicy, policy => policy
        .AddAuthenticationSchemes(CookieAuthenticationDefaults.AuthenticationScheme)
        .RequireAuthenticatedUser()
        .RequireRole(RbacPolicies.ManagerRole));
});

const string SpaCorsPolicy = "SpaCors";
var corsConfig = builder.Configuration.GetSection(BffCorsOptions.SectionName).Get<BffCorsOptions>()
    ?? throw new InvalidOperationException(
        $"The '{BffCorsOptions.SectionName}' configuration section is missing; cannot wire CORS.");

builder.Services.AddCors(options =>
    options.AddPolicy(SpaCorsPolicy, policy => policy
        .WithOrigins(corsConfig.SpaOrigin)
        .WithHeaders("Authorization", "Content-Type")
        .WithMethods("GET", "POST")
        .AllowCredentials()));

builder.Services.AddControllers();

var app = builder.Build();

app.UseCors(SpaCorsPolicy);
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Ok(new { status = "ok" }));
app.MapControllers();

app.Run();

public partial class Program;
