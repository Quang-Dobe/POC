# Infra — `infra/dev/` + `infra/prod/`

Two self-contained compose stacks that run the **app tier only** (frontend + backend, plus a
DEV-only Redis). The identity store (Keycloak), the secret store (OpenBAO), the Agent Gateway, and
DAB each live in their own sibling repo with their own compose file — this folder does NOT compose
them. The root `dev-up.ps1` / `prod-up.ps1` scripts orchestrate the full stack across those repos in
dependency order.

```
POC/                         # the projects sit as siblings under the repo root
  .env.example               # committed template — copied to .env.dev / .env.prod (both untracked)
  dev-up.ps1 / dev-down.ps1  # DEV orchestration: OpenBAO -> Keycloak -> app (-> Agent)
  prod-up.ps1 / prod-down.ps1#  PROD orchestration: app (-> Agent); no local identity/secret store
  POC.BFF/                   # .NET BFF + IDP-Simulator — build context for `backend`
  POC.FE/                    # React/Vite SPA served by nginx — build context for `frontend`
  POC.KeyCloak/              # DEV identity store (own compose, :8080)
  POC.OpenBao/               # DEV secret store (own compose, :8200)
  POC.Agent/                 # Agent Gateway (own compose, :8082; optional)
  POC.DAB/                   # Data API Builder (host process via `dab start`; not composed here)
  infra/
    dev/
      docker-compose.yml     #   backend (HTTPS via Kestrel) + redis + frontend (nginx HTTPS)
      nginx/default.conf      #   SPA fallback, listens 443 TLS
      tls/certs/              #   dev-tls.crt + dev-tls.key (self-signed; kept in sync by dev-up.ps1)
    prod/
      docker-compose.yml     #   backend (HTTP) + frontend (nginx HTTP) — talks to Azure AD + Key Vault
      nginx/default.conf      #   SPA fallback, listens 80
```

## Services (verified against the compose files)

### `infra/dev/docker-compose.yml`

| Service | Image / build | Host port | Notes |
|---|---|---|---|
| `backend` | build `../../POC.BFF` → `poc-backend` | `${BE_PORT}` → `8081` (default 5000) | `ENV=DEV`, `ASPNETCORE_ENVIRONMENT=Development`. Serves **HTTPS** directly via Kestrel (`ASPNETCORE_URLS=https://+:8081`) using the mounted `dev-tls.crt`/`.key`. `extra_hosts: localhost:host-gateway` so it reaches host Keycloak (`:8080`) and OpenBAO (`:8200`). Session store is `redis:6379`. `depends_on: redis`. |
| `redis` | `redis:7-alpine` | `6379` | Session store. Auth-less, no persistence (`--save "" --appendonly no`). |
| `frontend` | build `../../POC.FE` → `poc-frontend` | `${FE_PORT}` → container `443` (default 5173) | nginx serving the built SPA over **HTTPS**. `VITE_API_BASE_URL` baked at build time. Mounts `nginx/default.conf` and `tls/certs`. `depends_on: backend`. |

### `infra/prod/docker-compose.yml`

| Service | Image / build | Host port | Notes |
|---|---|---|---|
| `backend` | build `../../POC.BFF` → `poc-backend` | `${BE_PORT}` → `8081` (default 5000) | `ENV=PROD`, `ASPNETCORE_ENVIRONMENT=Production`. Plain **HTTP** (`ASPNETCORE_URLS=http://+:8081`) — TLS terminates at the ingress in real PROD. Reads secrets from Azure **Key Vault** (`KeyVault__VaultUri`) and authenticates with `AZURE_TENANT_ID`/`AZURE_CLIENT_ID`/`AZURE_CLIENT_SECRET`. Session store is managed Redis (`${REDIS_ADDRESS}`). `extra_hosts: localhost:host-gateway`. |
| `frontend` | build `../../POC.FE` → `poc-frontend-prod` | `${FE_PORT}` → container `80` (default 5173) | nginx serving the SPA over **HTTP**. `VITE_API_BASE_URL` baked at build time. Mounts `nginx/default.conf`. `depends_on: backend`. |

There is **no** `keycloak`, `openbao`, `agent`, `dab`, or `tls-terminator` service inside either
compose file. Those are separate stacks (see the sibling repos and the root scripts).

## DEV vs PROD differences

| | DEV | PROD |
|---|---|---|
| Identity | Keycloak (`POC.KeyCloak`, `https://localhost:8080`, realm `poc`) | Azure AD (`login.microsoftonline.com`) |
| Secrets | OpenBAO (`POC.OpenBao`, `http://localhost:8200`) | Azure Key Vault (`KeyVault__VaultUri`) |
| Backend scheme | HTTPS in-container (Kestrel + self-signed cert) | HTTP in-container (TLS at ingress) |
| Frontend nginx | listens `443` (TLS) | listens `80` |
| Redis | composed sibling `redis:7-alpine` (`redis:6379`) | managed Redis via `${REDIS_ADDRESS}` |
| Backend image | `poc-backend` | `poc-backend` |
| Frontend image | `poc-frontend` | `poc-frontend-prod` |
| Azure creds | none | `AZURE_TENANT_ID`/`AZURE_CLIENT_ID`/`AZURE_CLIENT_SECRET` |

## nginx (the `frontend` container)

Both nginx configs do the same one job: serve the built SPA and fall back to `index.html` so
client-side routes (e.g. the OIDC redirect landing) return the app instead of a 404
(`try_files $uri $uri/ /index.html`). nginx here is **not** a reverse proxy to the backend — the
browser calls the backend directly at `VITE_API_BASE_URL`. The only difference between the two:

- `dev/nginx/default.conf` — `listen 443 ssl;` with `ssl_certificate /etc/nginx/certs/dev-tls.crt`
  and `..._key /etc/nginx/certs/dev-tls.key` (the mounted `tls/certs`). Serves the SPA over HTTPS.
- `prod/nginx/default.conf` — `listen 80;`, no TLS. The ingress terminates TLS in real PROD.

Both are mounted into `/etc/nginx/conf.d/default.conf` at runtime (the FE Dockerfile does not bake
them).

## DEV TLS certs

`dev/tls/certs/dev-tls.crt` + `dev-tls.key` are the **self-signed dev cert**, used by BOTH the dev
`backend` (Kestrel) and the dev `frontend` (nginx) so the whole DEV surface is HTTPS. `dev-up.ps1`
keeps them in lockstep with the **trusted** ASP.NET dev cert: each run it compares thumbprints and
re-exports via `dotnet dev-certs https` if the mounted PEM is missing or stale. This matters because
DAB (a host process) fetches the BFF's OIDC metadata over HTTPS and validates the served cert against
the host trust store — a drifted cert means no JWKS and 401 on every token.

## Networks

Neither compose file declares an explicit network, so each stack uses its own Compose default bridge.
The dev `backend` and `frontend` share that bridge (the backend reaches `redis` by service name).
Cross-stack reachability (backend → host Keycloak/OpenBAO, DAB/Agent → backend) is over **host
ports**, enabled by `extra_hosts: ["localhost:host-gateway"]` — so no shared Compose network is
needed between repos.

## Configuration

Every host/port/URL lives once in the root env file (`.env.dev` or `.env.prod`, both copied from
`.env.example`). URLs are assembled from `*_HOST`/`*_PORT` primitives via Compose `${...}`
interpolation, so a port changes in one line. Key values from `.env.example`:

- `BE_PORT=5000`, `FE_PORT=5173`, `KEYCLOAK_PORT=8080`, `OPENBAO_PORT=8200`, `AGENT_PORT=8082`
- `AUTH_AUTHORITY=https://localhost:8080/realms/poc`, `VAULT_ADDRESS=http://localhost:8200`
- `VITE_API_BASE_URL=https://localhost:5000`, `CORS_SPA_ORIGIN=https://localhost:5173`
- PROD adds `KEYVAULT_VAULT_URI`, `TENANT_ID`, `AUTH_API_CLIENT_ID`, `AUTH_API_CLIENT_SECRET`,
  `REDIS_ADDRESS`

## Run

Use the root scripts — they pass `--env-file` and start everything in dependency order. Both stacks
publish the same host ports, so run **one environment at a time**.

```powershell
# One-time setup (from the POC root):
Copy-Item .env.example .env.dev    # DEV defaults work as-is
Copy-Item .env.example .env.prod   # then fill the Azure AD / Key Vault values

# DEV — OpenBAO -> Keycloak -> backend/frontend (syncs the dev TLS cert first):
.\dev-up.ps1                # add -WithAgent for the Agent Gateway, -WithDab to also run POC.DAB
.\dev-down.ps1              # tear down (reverse order); -Volumes also wipes volumes

# PROD-from-local — app only (Azure AD + Key Vault; no local identity/secret store):
.\prod-up.ps1              # add -WithAgent / -WithDab as above
.\prod-down.ps1            # tear down; -Volumes also wipes volumes
```

Run just this app stack (without the orchestration / store dependencies):

```powershell
docker compose -f infra/dev/docker-compose.yml  --env-file .env.dev  up --build
docker compose -f infra/prod/docker-compose.yml --env-file .env.prod up --build
```

### Endpoints

| Service | DEV | PROD |
|---|---|---|
| Frontend (nginx) | `https://localhost:5173` | `http://localhost:5173` |
| Backend (BFF) | `https://localhost:5000` (`/` liveness, `/api/message` gated) | `http://localhost:5000` |
| Redis | `localhost:6379` (composed) | managed (`REDIS_ADDRESS`) |
| Keycloak | `https://localhost:8080` (realm `poc`, `admin`/`admin`) | — (Azure AD) |
| OpenBAO | `http://localhost:8200` | — (Key Vault) |
| Agent Gateway | `http://localhost:8082` (optional) | `http://localhost:8082` (optional) |

## DEV test user (e2e login)

`testuser` / `Test1234!` — **DEV-ONLY**, shipped in `../POC.KeyCloak/realm-export.json`. Logs into
the throwaway local realm only; never reuse anywhere real.
