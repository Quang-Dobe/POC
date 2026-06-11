# Infra — `infra/dev/` + `infra/prod/`

Two self-contained compose stacks. The DEV identity/secret stores were extracted on 2026-06-08
into sibling projects **POC.KeyCloak** and **POC.OpenBao** (supersedes the in-folder
`compose.stores.yml` split of 2026-06-05, the original single-file Step G layout, and decision D3
"no separate PROD compose file"):

```
POC/                         # the three projects sit as siblings
  .env.dev                   # DEV env (untracked; from .env.example) — KEYCLOAK_*/OPENBAO_*/BAO_TOKEN + app keys
  .env.prod                  # PROD env (untracked) — AZURE_*/KEYVAULT_VAULT_URI + app keys + live AZURE_CLIENT_SECRET
  .env.example               # committed template for both files above
  POC.KeyCloak/              # DEV identity store (keycloak + realm-export.json) — own compose
  POC.OpenBao/               # DEV secret store (openbao + openbao-seed + seed.sh) — own compose
  POC.BFF/                   # .NET BFF + IDP-Simulator (app server) — build context for backend
  POC.FE/                    # React SPA — build context for frontend
  infra/
    dev/                     # DEV APP stack ONLY (frontend + backend + redis + tls-terminator) — not the stores
      docker-compose.yml     #   frontend (DEV VITE_* baked) + backend (ENV=DEV) + redis (session store)
                             #     + tls-terminator (scoped HTTPS edge); run with
                             #     --env-file ../../.env.dev (the root dev-up.ps1 passes it)
      tls/                   #   tls-terminator.conf + certs (DEV self-signed)
      nginx/
    prod/                    # PROD-from-local: frontend + backend vs REAL Entra + Key Vault + managed Redis
      docker-compose.yml     #   no keycloak/openbao/terminator — backend talks to Azure + managed Redis directly
      nginx/
```

Every host/port and config value lives once in its env file — DEV stores (`KEYCLOAK_*`,
`OPENBAO_*`, `BAO_TOKEN`) in `.env.dev`, Azure (`AZURE_*`, `KEYVAULT_VAULT_URI`) in `.env.prod`;
the app keys (`AUTH_*`, `VITE_*`, `*_HOST`/`*_PORT`) are **unprefixed and present in both** — the
file you load decides the environment. URL values are assembled from the `*_HOST`/`*_PORT`
primitives by compose's in-file `${...}` interpolation, so a port changes in exactly one line.

There is no umbrella: the root `dev-up.ps1` starts OpenBAO, then Keycloak, then this app — each from
its own sub-repo compose, in three separate compose projects. The backend reaches Keycloak
(`localhost:8080`) and OpenBAO (`localhost:8200`) over HOST ports (`extra_hosts: localhost:host-gateway`),
so no shared compose network is needed. `dev-down.ps1` tears them down.

## Run

```powershell
# Per-environment env files (copy once, from the POC root):
Copy-Item .env.example .env.dev    # DEV section — defaults work as-is
# copy the PROD section of .env.example into .env.prod and fill ids + secret (prod only)

# DEV — start the whole stack in order (OpenBAO -> Keycloak -> backend/frontend):
.\dev-up.ps1                       # from the POC root; -WithDab also launches POC.DAB
# (app compose alone: docker compose -f infra/dev/docker-compose.yml --env-file .env.dev up --build)

# PROD-from-local (self-contained in .env.prod)
docker compose -f infra/prod/docker-compose.yml --env-file .env.prod up --build
```

Both stacks publish the same host ports — run **one at a time**:

| Service | URL (host) | DEV | PROD |
|---|---|---|---|
| Frontend (nginx) | http://localhost:5173 | ✓ | ✓ |
| Backend (HTTP edge) | http://localhost:5000 (`BE_PORT`) — `/` liveness, browser `/auth`+`/api` (same scheme as FE so the SameSite=Lax session cookie attaches) | ✓ | ✓ |
| TLS terminator (nginx) | https://localhost:5001 (`BE_TLS_PORT`) — scoped HTTPS edge fronting the BFF: DAB OIDC discovery/JWKS + the top-level `/auth/callback`. DEV-only; PROD terminates TLS at the ingress | ✓ | — |
| Redis (session store) | localhost:6379 (`redis:6379` on the compose net) — DEV is auth-less, no persistence; PROD uses managed Redis (`REDIS_ADDRESS`, password via Key Vault) | ✓ | — |
| Keycloak | https://localhost:8080 — admin `admin`/`admin`, realm `poc` (shared w/ POC.DAB) | ✓ | — |
| OpenBAO | http://localhost:8200 — dev mode, KV v2 at `secret/` | ✓ | — |

## DEV boot order (enforced by `dev-up.ps1` — it waits for each stage before starting the next)

```
openbao (healthy) ─► openbao-seed (writes secrets, exits 0) ─┐
                                                             │   ┌─► frontend
                                       redis ────────────────┼─► backend ─┤
keycloak (healthy, realm imported) ──────────────────────────┘            └─► tls-terminator
```

## DEV test user (e2e login)

`testuser` / `Test1234!` — **DEV-ONLY**, shipped in `../POC.KeyCloak/realm-export.json`. Only logs
into the throwaway local realm; never reuse anywhere real.

## Sanctioned DEV-only committed exceptions (everything else: no secret values committed)

1. Root `.env.example` → `BAO_TOKEN=dev-only-token` — OpenBAO dev-mode root token placeholder
   (single secret-via-env exception, `secrets-dual-provider` §"Hard rules"). One value, read by
   both the store and the backend from the same `.env.dev` — they cannot drift.
2. `../POC.KeyCloak/realm-export.json` → `testuser` / `Test1234!` — DEV-only e2e login.

OpenBAO seed value (`Message--DisplayString`) is a non-secret DEV
placeholder written at seed time by `../POC.OpenBao/seed.sh` — non-empty on purpose (backend
fail-fasts on empty secrets).

## Trap mitigations (analyzed.md R2/R4 — why these exact settings)

- **Keycloak issuer host == browser host.** `KC_HOSTNAME=localhost` makes the issuer
  `https://localhost:8080/realms/poc` for the browser. The backend uses
  `extra_hosts: ["localhost:host-gateway"]` AND listens on container port **8081** (not 8080) so
  its discovery call to `localhost:8080` falls through to host Keycloak instead of connecting to
  itself — issuer matches `appsettings.DEV.json` byte-for-byte. The self-signed dev cert is trusted
  by a DEV-only backchannel handler in `Program.cs`. Mismatch = the classic "401 with a valid login"
  (`oidc-dual-idp`).
- **Audience mapper.** Realm export ships client scope `poc-api` (default on `poc-spa`) carrying
  `oidc-audience-mapper` with `included.custom.audience=poc-api` → token `aud` contains what the
  backend validates.
- **Keycloak healthcheck on internal http 8080** (KC 24.0; management port 9000 is KC 25+); **OpenBAO healthcheck
  via 127.0.0.1** (image resolves `localhost` → `::1` first, dev listener is IPv4-any).
- **Per-environment root env file.** `.env.dev` (DEV) / `.env.prod` (PROD) drive everything: FE
  values are baked as build args; BE values arrive as `Auth__*`/`KeyVault__*`/`Vault__*` env vars,
  which override the committed `appsettings.{ENV}.json` defaults (`Program.cs` re-adds
  `AddEnvironmentVariables()` above the ENV-keyed json — the documented layering json → env vars →
  secret store). `dev-up.ps1` passes `--env-file .env.dev` to every DEV stack, so the OpenBAO
  store's root token and the backend's `BAO_TOKEN` are literally the same line — no drift.
- **Scoped HTTPS edge, same-scheme browser edge.** Post-refactor the BE is a BFF + IDP-Simulator;
  the browser holds only an opaque httpOnly session cookie. DAB 2.0.8 reads `jwt.issuer`
  (`AUTH_DAB_ISSUER=https://localhost:5001`) as an OIDC Authority under `RequireHttpsMetadata`, so it
  fetches discovery/JWKS over HTTPS via the `tls-terminator` (`nginx:1.27-alpine`), which also fronts
  the top-level `/auth/callback`. The BE container stays HTTP on `:8081`; only this edge is TLS. The
  browser `/auth`+`/api` fetches hit the `http://localhost:5000` BE edge — **same scheme as the FE
  origin**, so Chromium attaches the `SameSite=Lax` session cookie to `credentials:'include'` (the
  https edge would be cross-scheme = cross-site and drop it). In PROD there is no terminator service —
  **TLS terminates at the ingress** and `AUTH_DAB_ISSUER` is the deployed BFF origin.
- **Redis is the session-store backing (Step D).** DEV runs `redis:7-alpine` as a compose sibling,
  reached by service name (`Session__RedisAddress: redis:6379`) overriding `appsettings.DEV.json`'s
  `localhost:6379` (unreachable from inside a container); it is auth-less with no persistence. PROD
  points `Session__RedisAddress` at a managed Redis (`${REDIS_ADDRESS}`, e.g. Azure Cache for Redis),
  password supplied via Key Vault (`Session:RedisPassword`), never inline.
- **No inbound JwtBearer audience.** The former `Auth__Audience` (and the retired
  `Agent__ClientId/Secret/TokenEndpoint/AgentScope` OBO keys) were removed from both compose files
  with `AddJwtBearer` + the Agent token providers (Steps F/G) — there is no inbound validator left to
  bind them. The Agent + DAB are trusted via the BE-minted RS256 token only.

## PROD Azure prerequisites (once per tenant)

- BFF app registration (the only OIDC client): **Web** platform, redirect URI
  `https://<bff-origin>/auth/callback` (the ingress TLS edge). The browser holds no token — the BFF
  runs the server-side authorization-code + PKCE flow. The backend authenticates to Key Vault with
  this app's credentials (`AZURE_CLIENT_ID`/`AZURE_CLIENT_SECRET`), so the app needs the **Key Vault
  Secrets User** role on the vault.
- No separate API / OBO app registration: the downstreams (Agent, DAB) trust the BFF's own
  issuer + JWKS, not an Entra `api://` scope.
- Key Vault secrets: `Message--DisplayString`, `IdpSimulator--SigningKeyPem` (the RS256 downstream
  signing key), and `Session--RedisPassword` (managed Redis).
