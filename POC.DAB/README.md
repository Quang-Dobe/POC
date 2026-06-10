# Practice.DAB — Fabric Warehouse + DAB with dual-IdP auth (Keycloak / Entra ID)

[Data API Builder](https://learn.microsoft.com/en-us/azure/data-api-builder/) (DAB) exposing a Microsoft Fabric **Warehouse** over **REST**, **GraphQL**, and **MCP** — configuration only, no application code. Two interchangeable environments selected by a single variable (`DAB_ENVIRONMENT`):

| | DEV (local) | PROD (container) |
|---|---|---|
| **L1: client → DAB** (who may call the API) | Keycloak (OIDC) | Entra ID (OIDC, separate tenant OK) |
| **L2: DAB → Fabric** (how data is read) | Entra **interactive** (browser sign-in) | Entra **service principal** (ClientId/Secret) |
| **Where DAB runs** | Host (`dab start`) | Docker container |

```
        Client (curl / SPA / agent)
            │  Bearer JWT  ── DEV: Keycloak token | PROD: Entra token   (L1)
            ▼
  http://localhost:8000/api | /graphql | /mcp     DAB engine
            │  TDS + Entra auth ── DEV: interactive | PROD: service principal   (L2)
            ▼
        Fabric Warehouse (NYC Taxi sample)
```

Key facts (full reasoning: [docs/idps-integration/idps-integration.analyzed.md](docs/idps-integration/idps-integration.analyzed.md)):

- **L2 is always Entra.** Fabric accepts Microsoft Entra auth only — Keycloak never reaches the database.
- **DEV DAB runs on the host, never in a container** — interactive auth opens a browser; containers can't.
- The L1 end-user identity is **not** passed to Fabric. Per-user filtering happens at the DAB layer via item-level policies on token claims (see [RBAC](#rbac--row-filtering)).
- If `DAB_ENVIRONMENT` is unset, DAB defaults to **Production** — the base config carries Entra (production-safe) auth, so a missing variable never yields an open API.

## Repository layout

```
├── dab-config.json                 # base: dwsql data-source, runtime, entities, RBAC, prod-safe auth
├── dab-config.Development.json     # override: L1 = Keycloak issuer/audience, dev mode (Swagger/Nitro)
├── dab-config.Production.json      # override: L1 = Entra issuer/audience, production mode
├── (no env files — DAB's env lives in the ROOT ../.env.dev + ../.env.prod; see ../.env.example)
├── Dockerfile                      # PROD DAB image (config baked in, secrets at runtime)
├── docker-compose.yml              # LOCAL: include:s the SHARED ../POC.KeyCloak (DAB runs on the host)
└── docs/idps-integration/          # requirement, decisions, Entra/Fabric setup guide

DAB's `poc-dab` client now lives in the SHARED realm `poc` in ../POC.KeyCloak/realm-export.json (one
Keycloak container + one realm serves both POC.Authentication's `poc-spa` and DAB's `poc-dab` over
https://localhost:8080; each validates its own audience — `poc-api` vs `poc-dab`).
```

DAB loads `dab-config.json`, then merges `dab-config.<DAB_ENVIRONMENT>.json` on top. Secrets resolve via `@env('VAR')` from the process environment or `.env`.

## Prerequisites

- .NET 8+ SDK and DAB CLI ≥ 2.0: `dotnet tool install -g Microsoft.DataApiBuilder`
- Microsoft Fabric workspace with capacity (trial works) + the sample Warehouse
  (Fabric portal → **New item** → **Sample warehouse**; NYC Taxi tables `Trip`, `Date`, `Geography`, `Weather` load automatically)
- Docker Desktop (Keycloak in DEV; the DAB image in PROD)

## Running DEV (local)

The L1 issuer is the SHARED Keycloak in `../POC.KeyCloak` (realm `poc`, https://localhost:8080).
One-time on a fresh clone — generate the cert it serves (DAB 2.0.8 refuses plain-HTTP OIDC metadata):

```powershell
dotnet dev-certs https -ep ..\POC.KeyCloak\certs\dev-cert.pfx -p changeit   # export the .NET dev cert
dotnet dev-certs https --trust                                              # lets DAB trust Keycloak's TLS
```

Then:

DAB's env now lives in the **root `..\.env.dev`** (the `POC.DAB (DEV)` block). Fill its
`FABRIC_CONN_STRING` (Fabric portal → Warehouse → Settings → SQL connection string), then from the
POC root run the orchestration script — it starts everything **and** loads `..\.env.dev` into the
process for `dab start`:

```powershell
.\dev-up.ps1 -WithDab    # OpenBAO + Keycloak + BE/FE, then loads ..\.env.dev and runs `dab start`
```

Manual (without the script) — `dab start` reads a file literally named `.env` (DotNetEnv), so copy
the root file in first:

```powershell
docker compose up -d                  # shared Keycloak (realm `poc`) on https://localhost:8080
Copy-Item ..\.env.dev .env            # DAB reads `.env`; the values live in the root .env.dev
dab start                             # DAB on the host :8000; browser opens for Fabric sign-in
```

> `.env.dev` no longer sets `ASPNETCORE_URLS`; `dev-up.ps1 -WithDab` defaults DAB to `:8000` (its
> built-in default `:5000` would clash with the backend's host `:5000`). For a manual `dab start`,
> set `$env:ASPNETCORE_URLS='http://localhost:8000'` first.

Get a token and call the API (test users are pre-baked, password = username):

```powershell
$token = (Invoke-RestMethod -Method Post `
  -Uri "https://localhost:8080/realms/poc/protocol/openid-connect/token" `
  -Body @{ grant_type="password"; client_id="poc-dab"; username="alice"; password="alice" }).access_token

curl "http://localhost:8000/api/Trip?`$first=5" -H "Authorization: Bearer $token" -H "X-MS-API-ROLE: reader"
```

> **`X-MS-API-ROLE` is required.** Without it DAB evaluates the request in the system `authenticated` role, which has no permissions here. The header value must match a role in the token's `roles` claim.

Dev mode extras: Swagger at `/swagger`, GraphQL playground (Nitro) at `/graphql`.

### Test users (Keycloak realm `poc`, client `poc-dab`)

| User | Password | Roles | `region` claim | Use for |
|---|---|---|---|---|
| alice | alice | reader | — | plain read access |
| bob | bob | manager | Manhattan County | row filtering (sees only Manhattan `Geography` rows) |
| carol | carol | manager | Queens County | row filtering (sees only Queens rows) |
| dave | dave | *(none)* | — | 403 check (valid token, no role) |

Keycloak admin console: `https://localhost:8080` (admin/admin).

> DAB's users + `poc-dab` client live in the SHARED realm `poc` (**`../POC.KeyCloak/realm-export.json`**; merged there with POC.Authentication's `poc-spa` when the Keycloak containers + realms merged). It is a **local-only seed** with plaintext throwaway credentials — never reuse it for a real environment. Keycloak's state is ephemeral (`down -v` wipes it; the file re-imports on next `up`); admin-console edits are lost unless exported back into the file.

## Running PROD (containerized)

One-time Entra/Fabric setup (app roles, service principal, tenant setting) — follow
**[docs/idps-integration/idps-integration.config-guide-line.md](docs/idps-integration/idps-integration.config-guide-line.md)**.

```powershell
# Fill the POC.DAB (PROD) block in the root ..\.env.prod (Entra + service-principal conn string).
docker build -t practice-dab .
docker run --rm --env-file ..\.env.prod -e DAB_ENVIRONMENT=Production -p 8000:5000 practice-dab
```

> `-p 8000:5000`, not `8000:8000` — the official DAB image listens on container port **5000** (`ASPNETCORE_URLS=http://+:5000` is baked into the base image).

`DAB_ENVIRONMENT` is passed with `-e` in addition to `.env` because config-file selection happens at process start; `.env` then satisfies the `@env(...)` lookups. Same endpoints, same entities, same RBAC — only the accepted tokens (Entra instead of Keycloak) and the Fabric auth method (service principal instead of interactive) change.

> `docker-compose.yml` deliberately runs **only Keycloak** (it `include:`s the shared `../POC.KeyCloak`). DEV DAB must stay on the host (browser), and PROD DAB runs from the Dockerfile image.

## Auth provider note (DAB 2.0.8)

Both environments use `"provider": "AzureAD"` — even for Keycloak. In DAB 2.0.8, `AzureAD`/`EntraID` is just generic JWT-bearer validation (issuer + audience + auto-JWKS), and the documented generic value `Custom` is **broken at runtime**: its auth scheme (`OAuthAuthentication`) is never registered, so every request 500s (verified against the 2.0.8 source). Revisit when upgrading DAB.

## RBAC & row filtering

DAB authorizes on a token claim named `roles` — both IdPs emit it (Keycloak via a realm-roles protocol mapper; Entra via App Roles). Role names are identical in both, so one permissions block serves both environments.

| Role | Trip / Date / Weather | Geography |
|---|---|---|
| *(no token)* | 403 — DAB treats a missing token as `anonymous`, which has no permissions | 403 |
| *(invalid/expired token)* | 401 | 401 |
| *(token, no role / wrong header)* | 403 | 403 |
| `reader` | read | read (all rows) |
| `manager` | read | read **filtered**: `County = token's region claim` |

The `manager` filter is an item-level policy in `dab-config.json` (the NYC Taxi sample's `Geography` is 50 rows, all `State='NY'`, split 10×5 across counties — so `County` is the column that demonstrates differential filtering):

```json
"policy": { "database": "@item.County eq @claims.region" }
```

DAB appends it to the SQL `WHERE` clause — the supported way to do per-user filtering, since every query reaches Fabric as the single L2 identity (no end-user passthrough, so database-native RLS can't see the caller).

## Exposed entities (read-only)

| Entity | Source | REST | GraphQL |
|---|---|---|---|
| Trip | `dbo.Trip` | `/api/Trip` | `trips` |
| Date | `dbo.Date` | `/api/Date` | `dateDims` |
| Geography | `dbo.Geography` | `/api/Geography` | `geographies` |
| Weather | `dbo.Weather` | `/api/Weather` | `weathers` |

Read-only is enforced by DAB permissions (`actions: ["read"]` — `dwsql` is read-optimized anyway). Fabric Warehouse tables have no enforced primary keys, so every entity declares explicit `source.key-fields`; without them DAB fails at startup with *"Primary key not configured"* (`Trip` uses a composite key of its five ID columns).

> The `Date` entity's GraphQL type is `DateDim` — naming it `Date` collides with GraphQL's built-in `Date` scalar and crashes schema building (*"The name `Date` was already registered by another type"*).

## MCP (AI agents)

MCP endpoint: `http://localhost:8000/mcp` (streamable HTTP). DAB exposes typed, read-only tools per entity. With auth enabled, MCP calls need the same bearer token **and** the `X-MS-API-ROLE` header as REST/GraphQL:

```powershell
$token = ...  # see "Running DEV" above
claude mcp add --transport http fabric-dab http://localhost:8000/mcp `
  --header "Authorization: Bearer $token" --header "X-MS-API-ROLE: reader"
```

> Tokens expire (10 min in the dev realm), so static-header registration is for quick tests only — a real agent integration needs an MCP client that refreshes tokens.

## Troubleshooting

| Symptom | Cause / fix |
|---|---|
| **500** on every request | Provider value DAB 2.0.8 can't wire (e.g. `Custom`) — log shows *"No authentication handler is registered for the scheme 'OAuthAuthentication'"*. Use `AzureAD`. |
| **500** + log *"MetadataAddress or Authority must use HTTPS"* | Issuer is plain HTTP. DAB 2.0.8 hardcodes `RequireHttpsMetadata=true` — Keycloak must be on HTTPS (see Running DEV). |
| **401** + log `IDX10500` *"No security keys were provided"* | DAB can't fetch Keycloak's JWKS — the dev cert isn't trusted yet. Run `dotnet dev-certs https --trust`. No DAB restart needed afterwards. |
| 401 with a valid-looking token | `issuer` mismatch — DAB's value must equal the token `iss` **byte-for-byte** (scheme, host, port, no trailing slash). Decode the token at jwt.io. |
| 403 with a valid token | Missing/wrong `X-MS-API-ROLE` header, or the role isn't in the token's `roles` claim. Check the claim is **top-level** (not nested under `realm_access`). |
| SQL error **18456** in the PROD container | Service principal rejected by Fabric: tenant setting "Service principals can use Fabric APIs" not enabled, SPN not added to the workspace, or wrong secret. Also: `User Id` must be the client ID **alone** (no `@tenantId`). See the [config guide](docs/idps-integration/idps-integration.config-guide-line.md). |
| Browser never opens in DEV | You containerized DAB. DEV DAB must run on the host (`dab start`). |
| Port clash on 8000/8080 | DAB owns 8000, Keycloak owns 8080. Stop strays: `docker ps`. |

## Status (2026-06-07)

**DEV end-to-end verified live** — every locally-runnable acceptance criterion passes:

- [x] Keycloak 24 on HTTPS :8080 (shared realm `poc`), realm auto-import, RS256 tokens (iss/aud/top-level `roles`/`region` all verified)
- [x] `dab start` → Fabric Warehouse via interactive Entra auth; `/health` 200
- [x] alice (`reader`) reads `Trip` over REST **and** GraphQL with a Keycloak token
- [x] dave (valid token, no role) → 403; no token → 403; garbage token → 401
- [x] Row filtering: bob sees only `Manhattan County` rows, carol only `Queens County` — same endpoint, REST and GraphQL
- [x] `dab validate` green for both environment merges; unset `DAB_ENVIRONMENT` falls back to Entra production auth
- [ ] PROD e2e — blocked on tenant prerequisites (Fabric admin setting + Entra app roles), see the [config guide](docs/idps-integration/idps-integration.config-guide-line.md)
