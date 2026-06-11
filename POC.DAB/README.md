# Practice.DAB — Fabric Warehouse + DAB, BFF-issued JWT auth

[Data API Builder](https://learn.microsoft.com/en-us/azure/data-api-builder/) (DAB) exposing a Microsoft Fabric **Warehouse** over **REST**, **GraphQL**, and **MCP** — configuration only, no application code. Two interchangeable environments selected by a single variable (`DAB_ENVIRONMENT`):

| | DEV (local) | PROD (container) |
|---|---|---|
| **L1: who DAB trusts** (who may call the API) | the **BFF** (BE IDP-Simulator), `AUTH_DAB_ISSUER=https://localhost:5001` | the **BFF origin** (`AUTH_DAB_ISSUER=https://<bff-origin>`) |
| **L2: DAB → Fabric** (how data is read) | Entra **interactive** (browser sign-in) | Entra **service principal** (ClientId/Secret) |
| **Where DAB runs** | Host (`dab start`) | Docker container |

```
        Client → BFF (BE IDP-Simulator)
            │  the BFF mints ONE internal JWT (aud=poc-internal, flat roles[] + region)
            ▼
        Agent gateway  ── forwards the SAME internal token on Authorization: Bearer
            │  Bearer = the BFF-minted internal token   (L1)
            ▼
  http://localhost:8000/api | /graphql | /mcp     DAB engine
            │  validates iss=AUTH_DAB_ISSUER, aud=AUTH_DAB_CLIENT_ID against the BFF's JWKS
            │  TDS + Entra auth ── DEV: interactive | PROD: service principal   (L2)
            ▼
        Fabric Warehouse (NYC Taxi sample)
```

Key facts (full reasoning: [docs/idps-integration/idps-integration.analyzed.md](docs/idps-integration/idps-integration.analyzed.md)):

- **DAB trusts ONE issuer: the BFF.** It does **not** validate Keycloak or Entra tokens directly. The BFF runs the external OIDC login (Keycloak in DEV, Entra in PROD), then **mints a single internal token** (`aud=poc-internal`) carrying a flat `roles[]` array and a `region` claim — the SAME token the Agent uses. A raw Keycloak/Entra access token presented to DAB is **rejected** — this is a hard cutover, no fallback path.
- **L2 is always Entra.** Fabric accepts Microsoft Entra auth only — the caller's identity never reaches the database.
- **DEV DAB runs on the host, never in a container** — interactive auth opens a browser; containers can't.
- The L1 end-user identity is **not** passed to Fabric. Per-user filtering happens at the DAB layer via item-level policies on the minted token's claims (see [RBAC](#rbac--row-filtering)).
- If `DAB_ENVIRONMENT` is unset, DAB defaults to **Production** — the base config carries the BFF-issuer auth (production-safe), so a missing variable never yields an open API.

## How L1 trust works (BFF as DAB's issuer)

DAB 2.0.8's `AzureAD` provider reads `runtime.host.authentication.jwt.issuer` as an **OIDC Authority** under `RequireHttpsMetadata=true`. So `AUTH_DAB_ISSUER` MUST be an **HTTPS URL**: DAB fetches `<issuer>/.well-known/openid-configuration`, reads the `jwks_uri` from it, and validates incoming tokens against the BFF's published signing keys.

- `jwt.issuer = @env('AUTH_DAB_ISSUER')` — the BFF's HTTPS OIDC-discovery base.
  - **DEV**: `https://localhost:5001` — the BFF's TLS edge, served by the nginx terminator in `infra/dev` (the BE container itself is plain HTTP; only this 5001 edge is HTTPS, precisely so DAB's `RequireHttpsMetadata` gate is satisfied).
  - **PROD**: the deployed **BFF origin**.
  - This value MUST byte-match the BFF's `IdpSimulator:Issuer` **and** the `iss` claim of the minted internal token (scheme, host, port, no trailing slash).
- `jwt.audience = @env('AUTH_DAB_CLIENT_ID')` — `poc-internal` in DEV; the DAB app's client ID / Application ID URI in PROD (mapped to the minted `aud` byte-for-byte). Must equal the minted token's `aud` and the BFF's `IdpSimulator:Audience`.

The minted token reaches DAB through the request chain, not a direct client call: the BFF mints ONE internal token and hands it to the agent gateway on `Authorization: Bearer`, and the agent forwards the SAME token to DAB on `Authorization: Bearer` without re-exchange and without a second header.

## Repository layout

```
├── dab-config.json                 # base: dwsql data-source, runtime, entities, RBAC, BFF-issuer auth (prod-safe)
├── dab-config.Development.json     # override: dev mode (Swagger/Nitro), CORS for the FE origin
├── dab-config.Production.json      # override: production mode
├── (no env files — DAB's env lives in the ROOT ../.env.dev + ../.env.prod; see ../.env.example)
├── Dockerfile                      # PROD DAB image (config baked in, secrets at runtime)
├── docker-compose.yml              # LOCAL: include:s the SHARED ../POC.KeyCloak (the BFF's login IdP; DAB runs on the host)
└── docs/idps-integration/          # requirement, decisions, Entra/Fabric setup guide
```

> Both `dab-config.Development.json` and `dab-config.Production.json` resolve `jwt.issuer`/`jwt.audience` from the SAME `AUTH_DAB_ISSUER` / `AUTH_DAB_CLIENT_ID` env vars as the base. The environments differ only in host **mode** (dev = Swagger/Nitro, CORS for `http://localhost:3000`) and the Fabric **L2** auth method — not in who issues L1 tokens.

DAB loads `dab-config.json`, then merges `dab-config.<DAB_ENVIRONMENT>.json` on top. Secrets resolve via `@env('VAR')` from the process environment or `.env`.

## Prerequisites

- .NET 8+ SDK and DAB CLI ≥ 2.0: `dotnet tool install -g Microsoft.DataApiBuilder`
- Microsoft Fabric workspace with capacity (trial works) + the sample Warehouse
  (Fabric portal → **New item** → **Sample warehouse**; NYC Taxi tables `Trip`, `Date`, `Geography`, `Weather` load automatically)
- Docker Desktop (the DAB image in PROD; in DEV the shared stack runs the BFF + its login IdP)

## Running DEV (local)

DAB's L1 issuer in DEV is the **BFF** at `https://localhost:5001` (the nginx TLS edge in `infra/dev`). DAB fetches the BFF's `/.well-known/openid-configuration` + JWKS over HTTPS, so the dev cert that edge serves must be trusted (DAB 2.0.8 refuses plain-HTTP or untrusted OIDC metadata):

```powershell
dotnet dev-certs https --trust    # lets DAB trust the BFF's dev TLS edge
```

DAB's env now lives in the **root `..\.env.dev`** (the `POC.DAB (DEV)` block). Fill its
`FABRIC_CONN_STRING` (Fabric portal → Warehouse → Settings → SQL connection string), then from the
POC root run the orchestration script — it starts everything **and** loads `..\.env.dev` into the
process for `dab start`:

```powershell
.\dev-up.ps1 -WithDab    # OpenBAO + the BFF (BE/FE) + its login IdP, then loads ..\.env.dev and runs `dab start`
```

Manual (without the script) — `dab start` reads a file literally named `.env` (DotNetEnv), so copy
the root file in first:

```powershell
docker compose up -d                  # shared stack (the BFF + its login IdP)
Copy-Item ..\.env.dev .env            # DAB reads `.env`; the values live in the root .env.dev
dab start                             # DAB on the host :8000; browser opens for Fabric sign-in
```

> `.env.dev` no longer sets `ASPNETCORE_URLS`; `dev-up.ps1 -WithDab` defaults DAB to `:8000` (its
> built-in default `:5000` would clash with the backend's host `:5000`). For a manual `dab start`,
> set `$env:ASPNETCORE_URLS='http://localhost:8000'` first.

### Getting a DAB token (DEV)

DAB only accepts the **BFF-minted internal token** (`aud=poc-internal`), not a Keycloak password-grant token. The normal path is end-to-end: the browser signs in through the BFF, the BFF mints ONE internal token and the agent forwards that SAME bearer to DAB on `Authorization: Bearer`. To exercise DAB directly with curl, drive the BFF login flow and capture the internal token the BFF issues (it carries the flat `roles[]` + `region` claims), then:

```powershell
$token = "<BFF-minted internal JWT (aud=poc-internal)>"
curl "http://localhost:8000/api/Trip?`$first=5" -H "Authorization: Bearer $token" -H "X-MS-API-ROLE: reader"
```

> **`X-MS-API-ROLE` is required.** Without it DAB evaluates the request in the system `authenticated` role, which has no permissions here. The header value must match a role in the token's `roles` claim.

Dev mode extras: Swagger at `/swagger`, GraphQL playground (Nitro) at `/graphql`.

### Roles & region in the minted token

The BFF derives these from the signed-in user and stamps them onto the DAB token:

| `roles` claim | `region` claim | Behaviour |
|---|---|---|
| `reader` | — | plain read access to every entity (all rows) |
| `manager` | e.g. `Manhattan County` | read access, with `Geography` rows filtered to `County = region` |
| `manager` | e.g. `Queens County` | read access, sees only `Queens County` rows |
| *(none)* | — | 403 (valid token, no role) |

> The external login identities (Keycloak users in DEV, Entra users in PROD) live with the BFF, not with DAB. DAB never sees them — it only ever validates the BFF-minted token's `iss`, `aud`, `roles`, and `region`.

## Running PROD (containerized)

One-time Entra/Fabric setup (app roles, service principal, tenant setting) — follow
**[docs/idps-integration/idps-integration.config-guide-line.md](docs/idps-integration/idps-integration.config-guide-line.md)**.

```powershell
# Fill the POC.DAB (PROD) block in the root ..\.env.prod: AUTH_DAB_ISSUER=https://<bff-origin>,
# AUTH_DAB_CLIENT_ID=<dab-app-id>, and the service-principal FABRIC_CONN_STRING.
docker build -t practice-dab .
docker run --rm --env-file ..\.env.prod -e DAB_ENVIRONMENT=Production -p 8000:5000 practice-dab
```

> `-p 8000:5000`, not `8000:8000` — the official DAB image listens on container port **5000** (`ASPNETCORE_URLS=http://+:5000` is baked into the base image).

`DAB_ENVIRONMENT` is passed with `-e` in addition to `.env` because config-file selection happens at process start; `.env` then satisfies the `@env(...)` lookups. Same endpoints, same entities, same RBAC, same L1 issuer (the BFF) — only `AUTH_DAB_ISSUER` (dev BFF edge vs the deployed BFF origin) and the Fabric L2 auth method (service principal instead of interactive) change.

> `docker-compose.yml` deliberately runs **only** the shared login stack (it `include:`s the shared `../POC.KeyCloak`, which is the BFF's external login IdP — not DAB's issuer). DEV DAB must stay on the host (browser), and PROD DAB runs from the Dockerfile image.

## Auth provider note (DAB 2.0.8)

Both environments use `"provider": "AzureAD"` — even though the issuer is the BFF. In DAB 2.0.8, `AzureAD`/`EntraID` is just generic JWT-bearer validation (issuer + audience + auto-JWKS via OIDC discovery), and the documented generic value `Custom` is **broken at runtime**: its auth scheme (`OAuthAuthentication`) is never registered, so every request 500s (verified against the 2.0.8 source). Revisit when upgrading DAB.

## RBAC & row filtering

DAB authorizes on a token claim named `roles`. The BFF emits it as a **flat array** on the DAB token (one permissions block serves both environments because the BFF normalizes role names identically).

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

`@claims.region` binds to the `region` claim the BFF stamps onto the minted DAB token. DAB appends the policy to the SQL `WHERE` clause — the supported way to do per-user filtering, since every query reaches Fabric as the single L2 identity (no end-user passthrough, so database-native RLS can't see the caller).

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

MCP endpoint: `http://localhost:8000/mcp` (streamable HTTP). DAB exposes typed, read-only tools per entity. With auth enabled, MCP calls need the same BFF-minted bearer token **and** the `X-MS-API-ROLE` header as REST/GraphQL:

```powershell
$token = "<BFF-minted internal JWT (aud=poc-internal)>"   # see "Getting a DAB token (DEV)" above
claude mcp add --transport http fabric-dab http://localhost:8000/mcp `
  --header "Authorization: Bearer $token" --header "X-MS-API-ROLE: reader"
```

> Tokens are short-lived, so static-header registration is for quick tests only — a real agent integration needs an MCP client that refreshes the BFF-minted token.

## Troubleshooting

| Symptom | Cause / fix |
|---|---|
| **500** on every request | Provider value DAB 2.0.8 can't wire (e.g. `Custom`) — log shows *"No authentication handler is registered for the scheme 'OAuthAuthentication'"*. Use `AzureAD`. |
| **500** + log *"MetadataAddress or Authority must use HTTPS"* | `AUTH_DAB_ISSUER` is plain HTTP. DAB 2.0.8 hardcodes `RequireHttpsMetadata=true` — the BFF's discovery base must be HTTPS (DEV: `https://localhost:5001`, the nginx TLS edge). |
| **401** + log `IDX10500` *"No security keys were provided"* | DAB can't fetch the BFF's JWKS — the dev cert on the 5001 edge isn't trusted yet. Run `dotnet dev-certs https --trust`. No DAB restart needed afterwards. |
| 401 with a valid-looking token | `issuer` mismatch — DAB's `AUTH_DAB_ISSUER` must equal the token `iss` **byte-for-byte** (scheme, host, port, no trailing slash) and the BFF's `IdpSimulator:Issuer`. Decode the token at jwt.io. |
| 401 with a raw Keycloak/Entra token | Expected — DAB trusts only BFF-minted tokens (hard cutover). Use the internal token (`aud=poc-internal`) issued by the BFF. |
| 403 with a valid token | Missing/wrong `X-MS-API-ROLE` header, or the role isn't in the token's `roles` claim. The BFF emits `roles` as a **top-level flat array**; confirm the value matches the header. |
| SQL error **18456** in the PROD container | Service principal rejected by Fabric: tenant setting "Service principals can use Fabric APIs" not enabled, SPN not added to the workspace, or wrong secret. Also: `User Id` must be the client ID **alone** (no `@tenantId`). See the [config guide](docs/idps-integration/idps-integration.config-guide-line.md). |
| Browser never opens in DEV | You containerized DAB. DEV DAB must run on the host (`dab start`). |
| Port clash on 8000 | DAB owns 8000. Stop strays: `docker ps`. |

## Status

**DEV end-to-end verified live** — every locally-runnable acceptance criterion passes:

- [x] BFF mints the single internal token (RS256, `aud=poc-internal`) carrying top-level flat `roles[]` + `region`; DAB validates `iss`/`aud` against the BFF's JWKS over the `https://localhost:5001` discovery edge
- [x] `dab start` → Fabric Warehouse via interactive Entra auth; `/health` 200
- [x] `reader` token reads `Trip` over REST **and** GraphQL
- [x] valid token with no role → 403; no token → 403; garbage token → 401; raw Keycloak/Entra token → 401
- [x] Row filtering: a `manager` token with `region=Manhattan County` sees only Manhattan rows; `region=Queens County` only Queens — same endpoint, REST and GraphQL
- [x] `dab validate` green for both environment merges; unset `DAB_ENVIRONMENT` falls back to production auth (still the BFF issuer)
- [ ] PROD e2e — blocked on tenant prerequisites (Fabric admin setting + Entra app roles), see the [config guide](docs/idps-integration/idps-integration.config-guide-line.md)
