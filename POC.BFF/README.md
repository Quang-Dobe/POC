# POC.BFF

A .NET 8 Backend-for-Frontend (BFF). The browser holds only an opaque, httpOnly session
cookie; the BFF owns the OIDC login flow server-side (authorization code + PKCE against
Keycloak in DEV / Entra ID in PROD), resolves roles from a config-driven RBAC map, and acts
as an internal IdP that **mints its own short-lived RS256 JWTs** for downstream calls (a Python
agent gateway). Secrets come from OpenBao in DEV and Azure Key Vault in PROD, selected by the
`ENV` variable. Feature code never branches on the provider — only configuration changes.

## Solution layout

`Poc.Bff.sln` contains four source projects under `src/` plus tests under `tests/`. The source
follows a strict layered architecture; references only ever point inward.

```
Poc.Bff.Domain  <-  Poc.Bff.Application  <-  Poc.Bff.Infrastructure  <-  Poc.Bff.Api
```

| Project | Responsibility |
|---|---|
| `Poc.Bff.Domain` | Pure domain types: RBAC (`RoleMap`, `RoleResolution`), invites (`InviteRequest`, `InviteGrant`, `IInviteStore`), downstream token claims. No external dependencies. |
| `Poc.Bff.Application` | CQRS handlers, the Result pattern, the in-house dispatch pipeline, FluentValidation validators, options classes, and the abstractions (interfaces) the Infrastructure layer implements. |
| `Poc.Bff.Infrastructure` | Implementations: OIDC discovery + code exchange, downstream token minting + signing keys, secret-store readers (OpenBao / Key Vault), Redis-backed sessions and data protection, invite provisioners (Keycloak / Entra), agent gateway HTTP client, SSE writer. |
| `Poc.Bff.Api` | ASP.NET Core controllers (thin: build a request, call the dispatcher, map the `Result`), `Program.cs` composition root, cookie auth + authorization policies, ProblemDetails mapping. |

Files are grouped by feature/aggregate (e.g. `Features/Auth/Callback/`), one public type per file.

## CQRS + Result dispatch

The Application layer uses a small in-house mediator (no MediatR). All cross-cutting concerns
run as pipeline behaviors around each handler.

- `IRequest<TResponse>` — marker for a command/query.
- `IRequestHandler<TRequest, TResponse>` — `Task<TResponse> Handle(request, ct)`.
- `ISender` — `Send<TResponse>(IRequest<TResponse>, ct)`; the concrete `Sender` resolves the
  handler and behaviors from DI by reflection and composes them into a delegate chain.
- `IPipelineBehavior<TRequest, TResponse>` — wraps the next delegate (`RequestHandlerDelegate<TResponse>`).

Handlers return `Result` / `Result<T>`. An `Error` carries `Code`, `Message`, and an `ErrorType`
(`None`, `Validation`, `NotFound`, `Conflict`, `Unexpected`, `Upstream`). Controllers map a failed
`Result` to RFC 7807 ProblemDetails via `Error.ToProblem()` (`ProblemDetailsMapper`).

Behaviors are registered in `Application/Common/DependencyInjection.cs` and execute outermost to
innermost in registration order:

```
Logging  ->  Validation  ->  Performance  ->  ExceptionHandling  ->  Handler
```

`AddApplication()` auto-registers every `IRequestHandler<,>` and every FluentValidation validator
in the assembly.

## Features

Each feature folder holds its command/query, validator, handler, and response.

| Area | Endpoints / handlers | Notes |
|---|---|---|
| **Auth** | `LoginQuery`, `CallbackCommand`, `MeQuery`, `LogoutCommand` | Login builds the authorize URL + PKCE correlation; callback validates `state`, exchanges the code, resolves roles, returns the session claims; me returns `{ displayName, roles }`; logout clears the cookie. |
| **Agent** | `AskCommand`, `StartAgentStreamCommand` | Mint a downstream token from session claims, then call the agent gateway; ask returns a single answer, stream returns an `IAsyncEnumerable<string>`. |
| **Invites** | `CreateInviteCommand` | Manager-only. Provisions a guest via the env-selected provisioner (Keycloak / Entra), returns a redeem URL. |
| **Message** | `GetMessageQuery` | Returns the secret-store-backed display message. |
| **Tokens** | `JwksQuery`, `OpenIdConfigQuery` | Publishes the BFF's public JWKS and a minimal OIDC discovery document so downstreams can validate the minted RS256 tokens. |

### HTTP endpoints

| Route | Method | Auth | Purpose |
|---|---|---|---|
| `/` | GET | none | Liveness — returns `{ status: "ok" }`. |
| `/auth/login` | GET | anonymous | Sets the PKCE correlation cookie, 302 to the IdP authorize URL. |
| `/auth/callback` | GET | anonymous | Code exchange + role resolution; signs in the cookie session, redirects to the SPA. |
| `/auth/me` | GET | session | `{ displayName, roles }`. |
| `/auth/logout` | POST | session | Signs out the cookie session. |
| `/auth/invite` | POST | manager role | Creates a guest invite. |
| `/api/message` | GET | session | The display message. |
| `/api/ask` | POST | reader/manager | Mints a downstream token, calls the agent, returns the answer. |
| `/api/ask/stream` | POST | reader/manager | `text/event-stream` SSE; each chunk written as a `data:` frame. |
| `/.well-known/jwks.json` | GET | anonymous | Public JWKS. |
| `/.well-known/openid-configuration` | GET | anonymous | Minimal OIDC discovery document. |

Authorization is cookie-based (no inbound bearer path). Policies are defined in `Program.cs`:
`session` (authenticated), `ask` (reader or manager role), `invite` (manager role).

## Authentication flow

```mermaid
sequenceDiagram
    actor User
    participant BFF as POC.BFF
    participant IdP as Keycloak (DEV) / Entra (PROD)
    participant Agent as Agent gateway

    User->>BFF: GET /auth/login
    BFF->>BFF: mint state + PKCE verifier + nonce; set correlation cookie
    BFF-->>User: 302 to IdP authorize URL
    User->>IdP: sign in (code + PKCE)
    IdP-->>BFF: 302 /auth/callback?code&state
    BFF->>BFF: verify state vs correlation cookie
    BFF->>IdP: exchange code (+ verifier) -> id_token
    BFF->>BFF: resolve roles[] + region from RoleMap (config-driven)
    BFF-->>User: sign in opaque session cookie, 302 to SPA

    User->>BFF: POST /api/ask (session cookie)
    BFF->>BFF: mint short-lived RS256 JWT from session claims
    BFF->>Agent: call with minted Bearer token
    Agent-->>BFF: answer
    BFF-->>User: 200 answer
```

The IdP's own role claims are **not** authoritative — roles and region come from the config
`RoleMap` (`InviteAwareRoleResolver` wrapping `ConfigRoleResolver`), keyed on the resolved subject.
Downstream tokens are signed with the RSA key from the secret store (`IdpSimulator` options) and
validated by downstreams against `/.well-known/jwks.json`. The agent gateway client streams by
reading the downstream's SSE response and re-emitting it through `SseWriter`.

## Configuration

Config loads in `Program.cs`: `appsettings.json` -> `appsettings.{ENV}.json` (ENV = `DEV` | `PROD`,
default `DEV`) -> environment variables -> the ENV-selected secret store. All config is consumed
through `IOptions<T>`; sections are validated on start.

| Options | Section | Key settings |
|---|---|---|
| `AuthOptions` | `Auth` | `Authority`, `ClientId`, `RedirectUri`, `FrontendReturnUrl`, `OidcClientSecret?` |
| `SessionOptions` | `Session` | `CookieName`, `TtlMinutes`, `RedisAddress` |
| `CorsOptions` | `Cors` | `SpaOrigin` |
| `DataProtectionOptions` | `DataProtection` | `ApplicationName`, `RedisKey` |
| `IdpSimulatorOptions` | `IdpSimulator` | `Issuer`, `Audience`, `DownstreamTokenTtlSeconds`, `SigningKeyId`, signing-key PEMs (current + next, secret-store backed) |
| `AgentOptions` | `Agent` | `Host`, `Port`, `AskTimeoutSeconds` |
| `MessageOptions` | `Message` | `DisplayString` (secret-store backed in PROD) |
| `InviteOptions` | `Invite` | `TenantId`, `DefaultRoles`, `DefaultRegion`, `DefaultPassword?` (secret-store backed) |
| `KeycloakAdminOptions` | `Keycloak` | `AdminBaseUrl`, `Realm`, `AdminClientId`, `AdminClientSecret?` |
| `RoleMap` | `RoleMap` | `Entries` (username -> roles + region) |

`Infrastructure/DependencyInjection.cs` selects providers by `ENV`: PROD uses `EntraInviteProvisioner`
and Key Vault; DEV uses `KeycloakInviteProvisioner` and OpenBao (and accepts the local self-signed
cert for the DEV IdP/Keycloak HTTP clients). Invited users are provisioned with the fixed default
password read from the secret store under `Invite:DefaultPassword` (DEV: seeded as
`Invite--DefaultPassword` in `POC.OpenBao/seed.sh`; PROD: a Key Vault secret named
`Invite--DefaultPassword`). The value is never placed in `appsettings`. Sessions, data-protection keys, and signing keys are
backed by Redis / the secret store. Set `DisableSecretStore=true` to skip the secret store (used by tests).

## Build and run

From `D:\Workspace\Github\POC` (repo root):

```powershell
dotnet build POC.BFF/Poc.Bff.sln          # build all projects
dotnet test  POC.BFF/Poc.Bff.sln          # run unit + integration tests
dotnet run --project POC.BFF/src/Poc.Bff.Api    # run the API (ENV defaults to DEV)
```

The API listens on Kestrel's default ports when run with `dotnet run`. In DEV the agent gateway is
expected at `localhost:8082`, Keycloak at `https://localhost:8080` (realm `poc`), Redis at
`localhost:6379`, and OpenBao at `http://localhost:8200` — see `src/Poc.Bff.Api/appsettings.DEV.json`.

### Docker

`Dockerfile` is a multi-stage build (`dotnet/sdk:8.0` build, `dotnet/aspnet:8.0` runtime, non-root
`app` user). The container listens on `http://+:8080` and `EXPOSE`s `8080`.

```sh
docker build -t poc-bff -f POC.BFF/Dockerfile POC.BFF
docker run -p 8080:8080 -e ENV=DEV poc-bff
```

## Key dependencies

FluentValidation (validation); `Microsoft.IdentityModel.Tokens` / `System.IdentityModel.Tokens.Jwt`
(RS256 minting + JWKS); `StackExchange.Redis` + `Microsoft.AspNetCore.DataProtection.StackExchangeRedis`
(sessions + key ring); `VaultSharp` (OpenBao); `Azure.Security.KeyVault.Secrets` + `Azure.Identity`
(Key Vault via `DefaultAzureCredential`).
