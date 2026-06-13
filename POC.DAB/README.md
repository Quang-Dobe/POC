# POC.DAB — Data API over a Fabric Warehouse

[Microsoft Data API Builder](https://learn.microsoft.com/en-us/azure/data-api-builder/) (DAB) exposing a Microsoft Fabric **Warehouse** as a read-only data API over **REST**, **GraphQL**, and **MCP**. This is configuration only — no application code. The API is consumed by the Agent / BFF side of the POC; DAB itself does not log users in, it only validates a JWT minted by the BFF and reads data from Fabric.

## Role in the POC

```
Client → BFF → Agent gateway → DAB → Fabric Warehouse
                  (Bearer JWT)   (REST / GraphQL / MCP on :8000)
```

- The **BFF** mints one internal JWT (`aud=poc-internal`) and the Agent forwards it to DAB on `Authorization: Bearer`.
- **DAB** validates the token (issuer + audience, via OIDC discovery against the BFF), applies role/region authorization, and queries Fabric.
- DAB authenticates to **Fabric** with a Microsoft Entra identity (interactive in DEV, service principal in PROD). The end-user identity never reaches the database — per-user filtering is done by DAB policies on the token claims.

## Endpoints

DAB listens on **:8000** locally and exposes three surfaces (paths from `dab-config.json`):

| Surface | Path | Notes |
|---|---|---|
| REST | `/api` | enabled |
| GraphQL | `/graphql` | enabled, introspection **off** |
| MCP | `/mcp` | enabled (streamable HTTP, for AI agents) |
| Health | `/health` | enabled, open to `anonymous` |

## Data source

A single Fabric Warehouse, referenced by connection string at runtime:

- `data-source.database-type`: **`dwsql`** (Fabric Data Warehouse / SQL analytics endpoint)
- `data-source.connection-string`: `@env('FABRIC_CONN_STRING')`

## Exposed entities (read-only)

All four entities map to `dbo.*` tables in the Warehouse and grant only the `read` action. (Sourced verbatim from `dab-config.json` — no other entities exist.)

| Entity | Table | REST | GraphQL type (singular / plural) | Key fields |
|---|---|---|---|---|
| Trip | `dbo.Trip` | `/api/Trip` | `Trip` / `Trips` | composite: `DateID`, `MedallionID`, `HackneyLicenseID`, `PickupTimeID`, `DropoffTimeID` |
| Date | `dbo.Date` | `/api/Date` | `DateDim` / `DateDims` | `DateID` |
| Geography | `dbo.Geography` | `/api/Geography` | `Geography` / `Geographies` | `GeographyID` |
| Weather | `dbo.Weather` | `/api/Weather` | `Weather` / `Weathers` | `DateID`, `GeographyID` |

Notes verified against the config:

- **`key-fields` are explicit** on every entity because Fabric Warehouse tables carry no enforced primary keys; without them DAB fails at startup.
- The **Date** entity's GraphQL type is named `DateDim`/`DateDims`, not `Date`, to avoid colliding with the built-in GraphQL `Date` scalar.

## RBAC and row filtering

Authorization is by the token's `roles` claim, plus a `region` claim for filtered access. Two roles are defined:

| Role | Trip / Date / Weather | Geography |
|---|---|---|
| `reader` | read (all rows) | read (all rows) |
| `manager` | read (all rows) | read, **filtered** by an item-level policy |

The `manager` policy on `Geography` is:

```json
"policy": { "database": "@item.County eq @claims.region" }
```

DAB appends this to the SQL `WHERE` clause so a `manager` only sees `Geography` rows whose `County` equals their `region` claim.

> REST/GraphQL/MCP calls must send the `X-MS-API-ROLE` header matching a role in the token's `roles` claim; otherwise DAB evaluates the request in a role with no permissions and returns 403.

## Configuration files

DAB loads `dab-config.json` (base), then merges `dab-config.<DAB_ENVIRONMENT>.json` on top. Secrets resolve via `@env('VAR')` from the process environment.

| File | Contents |
|---|---|
| `dab-config.json` | base: `dwsql` data source, runtime (REST/GraphQL/MCP/health), all entities, RBAC; host `mode: production`, empty CORS, `AzureAD` JWT auth |
| `dab-config.Development.json` | override: host `mode: development`, CORS allows `http://localhost:3000`, same `AzureAD` JWT auth |
| `dab-config.Production.json` | override: host `mode: production`, same `AzureAD` JWT auth |

### DEV vs PROD differences

| | Development | Production |
|---|---|---|
| Host `mode` | `development` (Swagger at `/swagger`, GraphQL playground) | `production` |
| CORS origins | `http://localhost:3000` | none (empty) |
| Fabric (L2) auth | Entra **interactive** (browser sign-in) | Entra **service principal** (client id + secret) |
| Where DAB runs | host (`dab start`) | Docker container |

> Both environments resolve `jwt.audience` / `jwt.issuer` from the **same** env vars and use `provider: AzureAD`. They differ only in host mode, CORS, and the Fabric connection auth method. If `DAB_ENVIRONMENT` is unset, DAB falls back to the base config (production-safe).

### Environment variables

These are read by `@env(...)` at runtime (defined in the repo-root `.env.dev` / `.env.prod`, see `../.env.example`):

| Variable | Purpose | DEV value | PROD value |
|---|---|---|---|
| `AUTH_INTERNAL_AUTHORITY` | OIDC authority / issuer DAB trusts (the BFF) | `https://localhost:5000` | the BFF origin |
| `AUTH_INTERNAL_AUDIENCE` | expected token `aud` | `poc-internal` | `poc-internal` |
| `FABRIC_CONN_STRING` | Fabric Warehouse connection (incl. Entra auth method) | `...Authentication=Active Directory Interactive...` | `...Authentication=Active Directory Service Principal;User Id=...;Password=...` |
| `DAB_ENVIRONMENT` | selects the override config to merge | `Development` | `Production` |

> The authority must be an **HTTPS** URL — DAB fetches `<authority>/.well-known/openid-configuration` to discover the BFF's JWKS, and it requires HTTPS metadata. The DEV BFF edge must serve a trusted dev cert (`dotnet dev-certs https --trust`).

## Running — DEV (on the host)

DEV uses interactive Fabric auth, which opens a browser, so DAB runs on the host, **not** in a container.

```powershell
# 1. trust the dev cert so DAB can read the BFF's OIDC metadata over HTTPS
dotnet dev-certs https --trust

# 2. DAB reads a file literally named .env; copy the root dev env in
Copy-Item ..\.env.dev .env

# 3. start DAB on :8000 (default :5000 clashes with the BFF)
$env:ASPNETCORE_URLS = 'http://localhost:8000'
dab start
```

Requires the .NET SDK and the DAB CLI: `dotnet tool install -g Microsoft.DataApiBuilder`.

## Running — PROD (container)

The `Dockerfile` bakes in the base + Production configs only; secrets are supplied at runtime via `--env-file`. The official DAB base image listens on container port **5000**.

```powershell
docker build -t poc-dab .
docker run --rm --env-file ..\.env.prod -e DAB_ENVIRONMENT=Production -p 8000:5000 poc-dab
```

> Publish with `-p <host>:5000` (the container listens on 5000). `DAB_ENVIRONMENT` is passed with `-e` as well as in `.env` because config-file selection happens at process start.

### docker-compose.yml

`docker-compose.yml` only `include:`s the shared `../POC.KeyCloak` stack (the BFF's external login IdP). It does **not** run DAB: DEV DAB runs on the host, and PROD DAB runs from the Dockerfile image.

## Smoke test (DEV)

```powershell
$token = "<BFF-minted internal JWT (aud=poc-internal)>"
curl "http://localhost:8000/api/Trip?`$first=5" `
  -H "Authorization: Bearer $token" `
  -H "X-MS-API-ROLE: reader"
```
