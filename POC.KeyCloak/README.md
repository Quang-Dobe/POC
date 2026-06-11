# POC.KeyCloak — SHARED DEV identity store

One Keycloak container, **one realm `poc`**, serving local apps over **HTTPS :8080**. Each app
validates its own audience against the same issuer:

| App | Client | Audience | Issuer | Used by |
|---|---|---|---|---|
| POC.BFF | `poc-bff` (PKCE) | `poc-bff` (id_token) | `https://localhost:8080/realms/poc` | BFF server-side OIDC login |
| POC.DAB | `poc-dab` (password grant) | `poc-dab` | `https://localhost:8080/realms/poc` | DAB L1 auth |

**Post-refactor, Keycloak (dev) is ONLY the external IDP for the BFF's server-side OIDC login.** The
BFF runs an authorization-code + PKCE flow against this realm using the public `poc-bff` client, then
reads identity from the validated `id_token`. Keycloak no longer issues tokens that any downstream
service trusts directly: **the BFF mints its own RS256 downstream tokens**, and the Agent and DAB
trust the **BFF issuer**, not Keycloak. (POC.DAB still uses Keycloak directly for its own L1
password-grant flow.)

HTTPS is mandatory because **POC.DAB (DAB 2.0.8) hardcodes `RequireHttpsMetadata=true`** and refuses
a plain-HTTP OIDC issuer. So port 8080 serves **HTTPS** (the port returned to 8080 on 2026-06-08; the
protocol stays https). POC.BFF and POC.DAB were merged onto this one realm/issuer.

```
POC.KeyCloak/
  docker-compose.yml    # keycloak 24.0, start-dev --import-realm, HTTPS :8080
  realm-export.json     # realm `poc`: clients poc-bff (BFF login, PKCE) + poc-dab (aud poc-dab, roles, region),
                        #   roles reader/manager, users testuser + alice/bob/carol/dave.
                        #   Also holds LEGACY/DEAD clients poc-spa, poc-api, agent-gateway (see below).
  certs/                # dev-cert.pfx (gitignored — regenerate per machine)
```

## One-time setup (fresh clone) — generate the TLS cert the container serves on 8080

```powershell
dotnet dev-certs https -ep ./certs/dev-cert.pfx -p changeit   # export the .NET dev cert (PKCS12)
dotnet dev-certs https --trust                                # optional: host browsers/.NET trust it (no -k)
```

## Run

```sh
# Standalone (from the POC root):
docker compose -f POC.KeyCloak/docker-compose.yml --env-file .env.dev up

# As part of the DEV stack — the root dev-up.ps1 starts this directly (OpenBAO -> here -> app).
# As POC.DAB's issuer — POC.DAB/docker-compose.yml `include:`s this file too.
```

| What | Value |
|---|---|
| URL | `https://localhost:8080` (self-signed .NET dev cert) |
| Admin console | `admin` / `admin` (DEV-only console cred, not a POC app secret) |
| Realm | `poc` (auto-imported on start) |
| POC.BFF test user | `testuser` / `Test1234!` (BFF login via client `poc-bff`) |
| POC.DAB test users | `alice`/`bob`/`carol`/`dave` (client `poc-dab`, password = username); roles `reader`/`manager`, region claim |

## Why these settings (do not "fix" them)

- **HTTPS on 8080 with the .NET dev cert.** DAB rejects HTTP issuers; the backend (POC.BFF)
  trusts this self-signed cert via a DEV-only backchannel handler in `Program.cs`. The container also
  listens on an **internal http:8081** (not published) purely so the healthcheck can probe `/health/ready`
  without TLS (a raw bash `/dev/tcp` GET can't speak TLS to the published 8080).
- **`KC_HOSTNAME=localhost` + `KC_HOSTNAME_STRICT=false`.** Forces the issuer host to `localhost`; the
  scheme/port come from the request, so https://localhost:8080 traffic yields the https issuer both apps
  validate byte-for-byte. Mismatch = the classic "401 with a valid login".
- **`poc-bff` is the BFF login client (public, PKCE-only, no DEV secret).** `standardFlowEnabled` with
  `pkce.code.challenge.method=S256`; redirectUri `https://localhost:5001/auth/callback` (the BFF's
  TLS-terminator callback edge). Default scopes `profile`/`email` carry `preferred_username` + `name`
  on the `id_token`; the BFF validates `aud==poc-bff` and reads identity from that token. The BFF maps
  identity to roles itself: **`RoleMap` is keyed on `preferred_username`** (`name` is the display name).
  **Roles are assigned by the BFF's config-driven `RoleMap`, NOT by Keycloak realm roles** — the realm
  `reader`/`manager` roles below are only for `poc-dab`'s own L1 flow.
- **`poc-dab` (DAB's own L1 auth).** Emits aud=`poc-dab` plus a TOP-LEVEL `roles` claim (DAB reads
  `roles`, not nested `realm_access.roles`) and a `region` claim for row-filtering. Its explicit
  `profile`/`email` scopes keep other audiences OFF its tokens (no audience cross-pollution).
- **Legacy/dead clients — `poc-spa`, `poc-api`, `agent-gateway`.** These are **unused post-cutover** and
  scheduled for removal, but **still physically present in `realm-export.json`** (do not assume they are
  gone). `poc-api` (confidential, issued `client_credentials` tokens with `aud=agent-gateway`) and
  `agent-gateway` (the OBO token-exchange target) belonged to the OLD model where Keycloak issued the
  tokens downstream services trusted. That path is dead: the BFF now mints its own RS256 downstream
  tokens. `poc-spa` was the SPA's direct PKCE client, superseded by the BFF `poc-bff` login flow.
- **Healthcheck probes internal http:8081** (KC 24.0 serves `/health/ready` on the http port; mgmt port
  9000 is KC 25+). The image has no curl/wget but ships bash, so it probes via bash `/dev/tcp`.

## Realm ownership

`realm-export.json` is the single source of truth (DAB's old `dab-poc` realm was merged in on
2026-06-08). Keycloak's own state is ephemeral (`down -v` wipes it; the file re-imports on next `up`);
admin-console edits are lost unless exported back into the file.
