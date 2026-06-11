# POC — workspace root

Sibling projects that compose into one DEV stack, plus a PROD-from-local stack:

| Project | Role |
|---|---|
| `POC.BFF/` | .NET **BFF + IDP-Simulator** (the app server) |
| `POC.FE/` | React SPA |
| `infra/` | `infra/dev` + `infra/prod` compose stacks orchestrating BFF + FE |
| `POC.KeyCloak/` | DEV **external IDP for BFF login only** — ONE Keycloak (https:8080), realm `poc`, public PKCE client `poc-bff`. (The old `poc-api`/`agent-gateway`/`poc-dab` clients are unused post-cutover) |
| `POC.OpenBao/` | DEV secret store (OpenBAO + seed) |
| `POC.Agent/` | Agent Gateway service; trusts the **BFF issuer only** (validates BFF-minted tokens against the BFF JWKS) |
| `POC.DAB/` | Data API Builder over Fabric; trusts the **BFF issuer** via OIDC discovery (`AUTH_DAB_ISSUER`). Not wired into the root `.env.dev`/`.env.prod` |

## Authentication model — BFF session + BE-as-IDP-Simulator

The backend is a **BFF + IDP-Simulator**. The browser logs in via a **server-side OIDC
authorization-code + PKCE flow the BFF owns** (`/auth/login`, `/auth/callback`, `/auth/me`,
`/auth/logout`). The browser holds **only an opaque httpOnly + Secure + SameSite=Lax session
cookie — no token, no claims**. Session state lives in **Redis**.

The BFF **mints short-lived (300s) RS256 JWTs per downstream audience** (`poc-agent`, `poc-dab`),
signed by an RSA-2048 key from the secret store, published at **`/.well-known/jwks.json`** (+ a
minimal **`/.well-known/openid-configuration`** for DAB). Claims are **flat `roles[]` + `region`**,
sourced from a **config-driven `RoleMap` keyed on `preferred_username`** — the external IDP's own
role claims are NOT authoritative.

- **POC.Agent** trusts the BFF issuer only and receives the BFF-minted DAB token via the
  **`X-Dab-Token`** header. The old OBO / `token_exchange.py` path is retired.
- **POC.DAB** trusts the BFF issuer (`AUTH_DAB_ISSUER` = `https://localhost:5001` in DEV, the BFF
  origin in PROD) via OIDC discovery; the `@claims.region` row filter binds to the minted `region`
  claim.
- **Keycloak (DEV) / Entra (PROD)** is only the external login IDP (the `poc-bff` public-PKCE
  client). Hard cutover (D8): **no fallback** to raw Entra/Keycloak token validation anywhere.
- There is also **SSE streaming** (`/api/ask/stream`) with a proactive session-extend that
  re-mints the downstream token mid-stream.

## Two env files — one per environment

Each environment has ONE root file; no project keeps its own `.env`. Both are gitignored.

| File | Holds | Committed? |
|---|---|---|
| `.env.dev` | **DEV only** — `KEYCLOAK_*`, `OPENBAO_*`, `BAO_TOKEN`, `VAULT_ADDRESS`, the app keys (`AUTH_AUTHORITY` = the external login IDP, `AUTH_DAB_ISSUER` = the BFF IDP-Simulator base, `AUTH_DAB_CLIENT_ID`, `VITE_API_BASE_URL`, `BE_TLS_PORT`, `AGENT_*`, `*_HOST`/`*_PORT`), **+ POC.DAB** (`DAB_ENVIRONMENT`, `FABRIC_CONN_STRING`). The browser holds no token, so there is no `VITE_OIDC_*` client config. | no (gitignored) |
| `.env.prod` | **PROD only** — `TENANT_ID` (feeds `AUTH_AUTHORITY`), `AZURE_CLIENT_ID`, `KEYVAULT_VAULT_URI`, `REDIS_ADDRESS` (managed Redis), the app keys, the live `AZURE_CLIENT_SECRET`, **+ POC.DAB** (`DAB_ENVIRONMENT`, `FABRIC_CONN_STRING`). | no (gitignored) |
| `.env.example` | committed template for both | **yes** |

Bootstrap: `cp .env.example .env.dev` (DEV defaults work as-is). For PROD, copy it to `.env.prod`
and switch to the `# PROD:` values + fill the prod-only ids + secret.

Naming: variables are **unprefixed and identical** across both files — the file you load
(`.env.dev` / `.env.prod`) decides the environment, not a `DEV_`/`PROD_` prefix.

## Run (from this root folder)

```powershell
# DEV — full stack, started in dependency order (OpenBAO -> Keycloak -> BFF infra/dev -> [Agent] -> [DAB]):
.\dev-up.ps1            # add -WithAgent to start the Agent Gateway, -WithDab to launch POC.DAB on the host (`dab start`)
.\dev-down.ps1          # stop the docker stacks

# Each sub-repo's compose can also run standalone (pass .env.dev):
docker compose -f POC.KeyCloak/docker-compose.yml --env-file .env.dev up
docker compose -f POC.OpenBao/docker-compose.yml  --env-file .env.dev up
docker compose -f infra/dev/docker-compose.yml --env-file .env.dev up --build  # app ONLY (BFF + frontend + redis + tls-terminator)
docker compose -f POC.Agent/docker-compose.yml --env-file .env.dev up --build                     # Agent Gateway

# PROD-from-local — real Entra + Key Vault (needs Azure). Self-contained in .env.prod.
docker compose -f infra/prod/docker-compose.yml --env-file .env.prod up --build
```

`infra/dev` no longer starts Keycloak/OpenBAO (the umbrella was removed) — it
brings up the app tier: the **BFF + frontend plus a `redis` session store and an `nginx`
`tls-terminator`** fronting the BFF on **:5001** (DAB needs HTTPS OIDC-discovery; it also fronts the
OIDC callback). The BFF container itself stays HTTP on `:8081`; browser `/auth` + `/api` fetches use
the `http://localhost:5000` edge (same scheme as the FE origin, so the SameSite=Lax cookie
attaches). **`dev-up.ps1` starts the stores first**, in order. Both app stacks publish the same host
ports (5173 / 5000) — run **one at a time**.

> The frontend's own `frontend/.env.example` is unrelated — it documents Vite's local
> `npm run dev` (`.env.development`/`.env.production`), not the docker stacks.

In DEV, OpenBAO's `seed.sh` now seeds **two** secrets into `secret/poc`: `Message--DisplayString`
(the non-secret app placeholder) and `IdpSimulator--SigningKeyPem` (a freshly generated, never
committed RSA-2048 PKCS#8 key the BFF loads to sign downstream tokens). The backend fail-fasts if
either is missing.

## Security note

`AZURE_CLIENT_SECRET` was previously in `infra/prod/.env` in plaintext. It now lives only in
`.env.prod`. **Rotate it in Entra** and treat the old value as compromised.

In PROD the BFF signing key (`IdpSimulator:SigningKeyPem`) and the Redis password
(`Session:RedisPassword`) come from Key Vault — never from an env file.
