# POC — Auth-Refactor Workspace Root

A multi-service proof-of-concept for **BFF-pattern authentication**. The backend owns
login server-side (OIDC authorization-code + PKCE), keeps the browser cookie-only, and
**mints its own short-lived JWTs** for each downstream service. One codebase runs against
local dev infrastructure or a PROD-from-local stack, switched only by which env file you load.

## What it demonstrates

- **BFF-pattern auth** — the .NET backend runs the OIDC code+PKCE flow against **Keycloak (DEV)**
  / **Azure Entra ID (PROD)**. The browser holds only an opaque httpOnly + Secure + SameSite=Lax
  **session cookie** — no token, no claims. Session state lives in **Redis**.
- **Downstream token minting** — the BFF mints short-lived RS256 JWTs per downstream audience,
  signed by an RSA key from the secret store and published at `/.well-known/jwks.json`
  (+ a minimal `/.well-known/openid-configuration` for DAB).
- **RBAC** — claims are flat `roles[]` + `region`, sourced from a config-driven `RoleMap`
  keyed on `preferred_username`. The external IDP's own role claims are not authoritative.
- **Invites** — the BFF provisions invited users via the Keycloak Admin REST API
  (`poc-admin-cli` service-account client).
- **OpenBao secrets** — the BFF reads secrets from **OpenBAO (DEV)** / **Azure Key Vault (PROD)**
  behind one seam; feature code never branches on the provider.
- **SSE streaming** — a Python agent streams answers over Server-Sent Events
  (`/ask/stream`), with a proactive session-extend that re-mints the downstream token mid-stream.
- **React FE** — a Vite SPA that talks only to the BFF edge.

## Architecture / topology

```
  Browser (React SPA)
        │  session cookie (httpOnly, SameSite=Lax)
        ▼
  ┌───────────────┐   OIDC code+PKCE login    ┌──────────────┐
  │   POC.BFF     │ ────────────────────────▶ │ Keycloak DEV │
  │  (.NET BFF +  │                            │  Entra PROD  │
  │ IDP-Simulator)│   reads secrets            └──────────────┘
  │               │ ────────▶ OpenBao DEV / Key Vault PROD
  └──────┬────────┘
         │ mints RS256 JWT per audience (JWKS published)
         ├───────────────▶ POC.Agent (Python, SSE) ──▶ POC.DAB (Fabric)
         └───────────────▶ POC.DAB (Data API Builder over Fabric)
```

| Service | Tech | DEV port | Role |
|---|---|---|---|
| POC.FE | React + Vite | 5173 (https) | SPA; talks only to the BFF edge |
| POC.BFF | .NET | 5000 (https) | BFF + IDP-Simulator; owns login, mints downstream JWTs, publishes JWKS |
| Keycloak | Keycloak | 8080 (https) | DEV external OIDC IdP (realm `poc`, public PKCE client `poc-bff`) |
| OpenBAO | OpenBAO | 8200 (http) | DEV secret store (KV v2) + one-shot seed; stand-in for Key Vault |
| POC.Agent | Python / FastAPI | 8082 (http) | Agent Gateway; validates BFF-minted tokens via JWKS; `/ask` + `/ask/stream` SSE |
| POC.DAB | Data API Builder | 8000 (http) | REST/GraphQL/MCP over a Fabric Warehouse; trusts the BFF issuer; `region` row filter |
| redis | Redis 7 | 6379 | BFF session store (DEV) |

In PROD, identity is Azure AD and secrets come from Azure Key Vault — there is no local
Keycloak / OpenBAO. POC.DAB runs on the host (`dab start`), not in the docker stack.

## Repo layout

| Folder | Purpose |
|---|---|
| `POC.BFF/` | .NET BFF + IDP-Simulator — owns server-side login, mints downstream JWTs, reads the secret store |
| `POC.Agent/` | Python Agent Gateway — validates BFF tokens against JWKS; serves `/ask` and `/ask/stream` (SSE) |
| `POC.DAB/` | Microsoft Data API Builder over a Fabric Warehouse; trusts the BFF issuer (config only, no app code) |
| `POC.KeyCloak/` | DEV OIDC identity provider — one Keycloak, realm `poc`, public PKCE client `poc-bff` |
| `POC.OpenBao/` | DEV secret store — OpenBAO (dev mode, KV v2) + a one-shot seed |
| `POC.FE/` | React + Vite SPA |
| `infra/` | `infra/dev` + `infra/prod` docker-compose stacks for the app tier (BFF + FE + redis) |

## Env files

Each environment has ONE root file; no project keeps its own `.env`. Both are gitignored.
Variables are unprefixed and identical across both — the file you load decides the environment.

| File | Holds | Committed? |
|---|---|---|
| `.env.dev` | DEV values — `KEYCLOAK_*`, `OPENBAO_*`, `BAO_TOKEN`, `VAULT_ADDRESS`, app keys, `AGENT_*`, DAB config | no (gitignored) |
| `.env.prod` | PROD values — `TENANT_ID`, `AZURE_CLIENT_ID`/`SECRET`, `KEYVAULT_VAULT_URI`, managed `REDIS_ADDRESS`, DAB config | no (gitignored) |
| `.env.example` | committed template for both | yes |

Bootstrap: `Copy-Item .env.example .env.dev` (DEV defaults work as-is). For PROD, copy it to
`.env.prod` and fill the Azure AD / Key Vault values.

## Run (from this root folder)

The four PowerShell scripts orchestrate the whole stack in dependency order.

```powershell
# DEV — OpenBAO -> Keycloak -> BFF + FE  (one-time prereqs: copy .env.dev + trust the dev cert)
.\dev-up.ps1                 # docker stacks only
.\dev-up.ps1 -WithAgent      # also start the Agent Gateway container
.\dev-up.ps1 -WithDab        # also launch POC.DAB on the host (`dab start`; needs Fabric + a browser)
.\dev-down.ps1               # stop the DEV docker stacks
.\dev-down.ps1 -Volumes      # also wipe volumes

# PROD-from-local — real Entra + Key Vault (needs Azure); no local Keycloak/OpenBAO
.\prod-up.ps1                # docker app stack only (BFF + FE from infra\prod)
.\prod-up.ps1 -WithAgent     # also start the Agent Gateway container
.\prod-up.ps1 -WithDab       # also launch POC.DAB on the host
.\prod-down.ps1              # stop the PROD docker stacks
.\prod-down.ps1 -Volumes     # also wipe volumes
```

Script flags (from the `param(...)` blocks):

| Script | Flags |
|---|---|
| `dev-up.ps1` | `-WithDab`, `-WithAgent` |
| `dev-down.ps1` | `-Volumes` |
| `prod-up.ps1` | `-WithDab`, `-WithAgent` |
| `prod-down.ps1` | `-Volumes` |

`dev-up.ps1` also keeps the mounted dev-TLS PEM in lockstep with the trusted .NET dev cert
(it re-exports when the thumbprint drifts), starts OpenBAO first and polls it healthy, then
brings up Keycloak and the app tier. `-WithAgent` is also implied when `AGENT_GATEWAY_ENABLED=true`.
POC.DAB (DEV and PROD) always runs as a host process — stop it with Ctrl+C in its own window.

Each sub-repo's compose can also run standalone with `--env-file .env.dev`. The DEV and PROD app
stacks publish the same host ports (5173 / 5000) — run one at a time.

## Security note

`AZURE_CLIENT_SECRET` lives only in `.env.prod` (gitignored). In PROD the BFF signing key
(`IdpSimulator:SigningKeyPem`) and the Redis password (`Session:RedisPassword`) come from
Key Vault — never from an env file.
