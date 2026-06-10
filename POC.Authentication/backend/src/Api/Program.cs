using Api.Agents;
using Api.Configuration;
using Api.Endpoints;
using Api.Secrets;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

var env = builder.Configuration["ENV"] ?? "DEV";

builder.Configuration.AddJsonFile($"appsettings.{env}.json", optional: false, reloadOnChange: false);
builder.Configuration.AddEnvironmentVariables();
builder.Configuration.AddEnvSelectedSecretStore(env, builder.Configuration);

builder.Services.AddOptions<AuthOptions>()
    .Bind(builder.Configuration.GetSection(AuthOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddOptions<CorsOptions>()
    .Bind(builder.Configuration.GetSection(CorsOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddOptions<MessageOptions>()
    .Bind(builder.Configuration.GetSection(MessageOptions.SectionName));
builder.Services.AddOptions<AgentOptions>()
    .Bind(builder.Configuration.GetSection(AgentOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddHttpClient<IAgentGatewayClient, AgentGatewayClient>((sp, http) =>
{
    var opts = sp.GetRequiredService<IOptions<AgentOptions>>().Value;
    http.BaseAddress = new Uri($"http://{opts.Host}:{opts.Port}");
});

builder.Services.AddHttpClient("AgentTokenClient")
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback = env == "DEV"
            ? HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            : null
    });

builder.Services.AddSingleton<IAgentTokenProvider>(sp =>
{
    var opts = sp.GetRequiredService<IOptions<AgentOptions>>();
    var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient("AgentTokenClient");
    return env == "DEV"
        ? new KeycloakAgentTokenProvider(opts, http)
        : (IAgentTokenProvider)new EntraAgentTokenProvider(opts, http);
});

const string SpaCorsPolicy = "SpaCors";
var authOptions = builder.Configuration.GetSection(AuthOptions.SectionName).Get<AuthOptions>()
    ?? throw new InvalidOperationException(
        $"The '{AuthOptions.SectionName}' configuration section is missing; cannot wire JwtBearer.");
var corsOptions = builder.Configuration.GetSection(CorsOptions.SectionName).Get<CorsOptions>()
    ?? throw new InvalidOperationException(
        $"The '{CorsOptions.SectionName}' configuration section is missing; cannot wire CORS.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.Authority = authOptions.Authority;
        o.Audience = authOptions.Audience;
        o.RequireHttpsMetadata = authOptions.RequireHttpsMetadata;

        if (env == "DEV")
        {
            o.BackchannelHttpHandler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback =
                    HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
            };
        }
    });
builder.Services.AddAuthorization();
builder.Services.AddCors(options =>
    options.AddPolicy(SpaCorsPolicy, policy => policy
        .WithOrigins(corsOptions.SpaOrigin)
        .WithHeaders("Authorization", "Content-Type")
        .WithMethods("GET", "POST")));

var app = builder.Build();

app.UseCors(SpaCorsPolicy);
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Ok(new { status = "ok" }));
app.MapMessageEndpoints();
app.MapAgentEndpoints();
app.Run();

public partial class Program;
