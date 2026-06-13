# POC.KeyCloak — OIDC Identity Provider (dev)

One Keycloak container, one realm `poc`, serving local apps over **HTTPS :8080**.

**Role:** Keycloak (dev) is the external OIDC identity provider for **POC.BFF's** server-side
login. The BFF runs an authorization-code + PKCE flow against this realm using the public
`poc-bff` client, then reads identity from the validated `id_token`. The BFF also calls the
Keycloak Admin REST API (via the `poc-admin-cli` service-account client) to provision invited
users. Keycloak does not issue tokens that downstream services trust directly — the BFF mints
its own RS256 downstream tokens.

HTTPS is mandatory because the BFF sets `RequireHttpsMetadata=true` and refuses a plain-HTTP
OIDC issuer. So the published port 8080 serves **HTTPS**.

```
POC.KeyCloak/
  docker-compose.yml    keycloak 24.0, start-dev --import-realm, HTTPS :8080 (+ internal http :8081)
  realm-export.json     realm `poc`: clients poc-bff / poc-spa / poc-admin-cli,
                        roles reader + manager, users alice + bob + service account
  certs/                dev-cert.pfx (gitignored — regenerate per machine)
```

## What `realm-export.json` defines (verified against the file)

**Realm:** `poc` — `sslRequired: none`, RS256 signatures, access-token lifespan 600s,
registration disabled, login-with-email allowed.

**Clients**

| clientId | Type | Flows | redirectUri / webOrigins | Notes |
|---|---|---|---|---|
| `poc-bff` | public (PKCE-only, no secret) | standard flow (auth code) | `https://localhost:5000/auth/callback` / `https://localhost:5000` | The BFF's server-side OIDC login client. `pkce.code.challenge.method=S256`, post-logout `https://localhost:5000/*`. Default scopes `profile`, `email`. BFF validates `aud==poc-bff`. |
| `poc-spa` | public (PKCE) | standard flow + direct access grants | `https://localhost:5173/*` / `https://localhost:5173` | SPA direct-PKCE client. Default scopes `profile`, `email`. |
| `poc-admin-cli` | confidential (service account) | client_credentials only | — | Used by the BFF for the Keycloak Admin REST invite flow. Service account holds `realm-management` roles `manage-users` + `view-users` (least privilege). The realm file carries a DEV secret `poc-admin-cli-dev-secret`; in the BFF this secret is read from the store seam (`Keycloak--AdminClientSecret`), not committed config. |

**Realm roles**

| role | Description (from file) |
|---|---|
| `reader` | DAB: may read all exposed entities |
| `manager` | DAB: may read entities; Geography rows filtered by the `region` claim |

**Client scopes:** `profile` and `email` are explicitly defined. `profile` carries the
`username -> preferred_username` mapper and a full-name mapper (both onto the `id_token`), so logins
surface `preferred_username` and `name`.

**Users**

| username | Password | Realm roles | Attributes |
|---|---|---|---|
| `alice` | `alice` | `reader` | — |
| `bob` | `bob` | `manager` | `region = Manhattan County` |
| `service-account-poc-admin-cli` | — | `realm-management`: `manage-users`, `view-users` | service account for `poc-admin-cli` |

(Passwords equal the username; both users are non-temporary.)

## One-time setup (fresh clone) — generate the TLS cert served on 8080

```powershell
dotnet dev-certs https -ep ./certs/dev-cert.pfx -p changeit   # export the .NET dev cert (PKCS12)
dotnet dev-certs https --trust                                # optional: trust it on this host
```

The keystore password (`changeit`) and path are fixed by `docker-compose.yml`.

## Run

```sh
# From the POC root:
docker compose -f POC.KeyCloak/docker-compose.yml up
```

The container runs `start-dev --import-realm`, so `realm-export.json` is auto-imported on start
(mounted read-only at `/opt/keycloak/data/import/`).

| What | Value |
|---|---|
| URL | `https://localhost:8080` (self-signed .NET dev cert) |
| Published port | `${KEYCLOAK_PORT:-8080}` -> container 8080 (HTTPS) |
| Admin console | `admin` / `admin` (DEV-only console login, not a POC app secret) |
| Realm | `poc` (auto-imported on start) |
| Issuer | `https://localhost:8080/realms/poc` |
| Login users | `alice`/`alice` (reader), `bob`/`bob` (manager) |

## How the BFF connects

The BFF (`POC.BFF/src/Poc.Bff.Api/appsettings.DEV.json`) is configured with:

- **Authority** `https://localhost:8080/realms/poc` — this realm's issuer.
- **ClientId** `poc-bff` with **RedirectUri** `https://localhost:5000/auth/callback`.
- **RequireHttpsMetadata** `true` — hence HTTPS on 8080.
- **AdminBaseUrl** `https://localhost:8080` + **AdminClientId** `poc-admin-cli` for the invite
  provisioner; the admin client secret comes from the secret store, not from this repo.

## Why these settings (do not "fix" them)

- **HTTPS on 8080 with the .NET dev cert.** The BFF rejects HTTP issuers. The container also
  listens on an **internal http:8081** (not published) purely so the healthcheck can probe
  `/health/ready` without TLS (a raw bash `/dev/tcp` GET cannot speak TLS to 8080).
- **`KC_HOSTNAME=localhost` + `KC_HOSTNAME_STRICT=false`.** Forces the issuer host to `localhost`;
  scheme/port come from the request, so `https://localhost:8080` traffic yields the https issuer
  the BFF validates byte-for-byte. Mismatch = the classic "401 with a valid login".
- **`poc-bff` is public, PKCE-only, no secret.** `standardFlowEnabled` with
  `pkce.code.challenge.method=S256`. The BFF validates `aud==poc-bff` and reads identity from the
  `id_token`. Roles are assigned by the BFF, NOT by Keycloak realm roles — the `reader`/`manager`
  realm roles are descriptive (DAB-oriented) and not consumed by the BFF login.
- **Healthcheck probes internal http:8081** (KC 24.0 serves `/health/ready` on the http port; mgmt
  port 9000 is KC 25+). The image has no curl/wget but ships bash, so it probes via bash `/dev/tcp`.
- **`KC_FEATURES=token-exchange`** is enabled on the container.

## Realm ownership

`realm-export.json` is the single source of truth. Keycloak's own state is ephemeral
(`down -v` wipes it; the file re-imports on next `up`); admin-console edits are lost unless
exported back into the file.
