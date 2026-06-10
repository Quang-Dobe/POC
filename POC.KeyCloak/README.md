# POC.KeyCloak — SHARED DEV identity store

One Keycloak container, **one realm `poc`**, serving **both** local apps over **HTTPS :8080**. The
realm holds two clients; each app validates its own audience against the same issuer:

| App | Client | Audience | Issuer | Used by |
|---|---|---|---|---|
| POC.Authentication | `poc-spa` (PKCE) | `poc-api` | `https://localhost:8080/realms/poc` | SPA + backend |
| POC.DAB | `poc-dab` (password grant) | `poc-dab` | `https://localhost:8080/realms/poc` | DAB L1 auth |

HTTPS is mandatory because **POC.DAB (DAB 2.0.8) hardcodes `RequireHttpsMetadata=true`** and refuses
a plain-HTTP OIDC issuer. So port 8080 serves **HTTPS** (the port returned to 8080 on 2026-06-08; the
protocol stays https). POC.Authentication and POC.DAB were merged onto this one realm/issuer.

```
POC.KeyCloak/
  docker-compose.yml    # keycloak 24.0, start-dev --import-realm, HTTPS :8080
  realm-export.json     # realm `poc`: clients poc-spa (aud poc-api) + poc-dab (aud poc-dab, roles, region),
                        #   roles reader/manager, users testuser + alice/bob/carol/dave
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
| POC.Authentication test user | `testuser` / `Test1234!` (client `poc-spa`) |
| POC.DAB test users | `alice`/`bob`/`carol`/`dave` (client `poc-dab`, password = username); roles `reader`/`manager`, region claim |

## Why these settings (do not "fix" them)

- **HTTPS on 8080 with the .NET dev cert.** DAB rejects HTTP issuers; the backend (POC.Authentication)
  trusts this self-signed cert via a DEV-only backchannel handler in `Program.cs`. The container also
  listens on an **internal http:8081** (not published) purely so the healthcheck can probe `/health/ready`
  without TLS (a raw bash `/dev/tcp` GET can't speak TLS to the published 8080).
- **`KC_HOSTNAME=localhost` + `KC_HOSTNAME_STRICT=false`.** Forces the issuer host to `localhost`; the
  scheme/port come from the request, so https://localhost:8080 traffic yields the https issuer both apps
  validate byte-for-byte. Mismatch = the classic "401 with a valid login".
- **One realm, two clients, two audiences.** `poc-spa` carries client scope `poc-api` (aud=`poc-api`);
  `poc-dab` emits aud=`poc-dab` plus a TOP-LEVEL `roles` claim (DAB reads `roles`, not nested
  `realm_access.roles`) and a `region` claim for row-filtering. `poc-dab`'s explicit `profile`/`email`
  scopes keep `poc-api` OFF its tokens (no audience cross-pollution).
- **Healthcheck probes internal http:8081** (KC 24.0 serves `/health/ready` on the http port; mgmt port
  9000 is KC 25+). The image has no curl/wget but ships bash, so it probes via bash `/dev/tcp`.

## Realm ownership

`realm-export.json` is the single source of truth (DAB's old `dab-poc` realm was merged in on
2026-06-08). Keycloak's own state is ephemeral (`down -v` wipes it; the file re-imports on next `up`);
admin-console edits are lost unless exported back into the file.
