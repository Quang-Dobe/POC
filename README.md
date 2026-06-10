# POC — workspace root

Sibling projects that compose into one DEV stack, plus a PROD-from-local stack:

| Project | Role |
|---|---|
| `POC.Authentication/` | React SPA + .NET API (the app); `infra/dev` + `infra/prod` compose stacks |
| `POC.KeyCloak/` | DEV identity store — ONE Keycloak (https:8080), ONE realm `poc` serving both POC.Authentication (client `poc-spa`) and POC.DAB (client `poc-dab`) |
| `POC.OpenBao/` | DEV secret store (OpenBAO + seed) |
| `POC.DAB/` | Data API Builder over Fabric; shares the POC.KeyCloak realm `poc` (client `poc-dab`). Not wired into the root `.env.dev`/`.env.prod` |

## Two env files — one per environment

Each environment has ONE root file; no project keeps its own `.env`. Both are gitignored.

| File | Holds | Committed? |
|---|---|---|
| `.env.dev` | **DEV only** — `KEYCLOAK_*`, `OPENBAO_*`, `BAO_TOKEN`, `VAULT_ADDRESS`, the app keys (`AUTH_AUTHORITY`, `AUTH_API_AUDIENCE`, `AUTH_DAB_CLIENT_ID`, `VITE_*`, `*_HOST`/`*_PORT`), **+ POC.DAB** (`DAB_ENVIRONMENT`, `FABRIC_CONN_STRING`). DAB shares `AUTH_AUTHORITY` + `AUTH_DAB_CLIENT_ID`. | no (gitignored) |
| `.env.prod` | **PROD only** — `TENANT_ID` (feeds `AUTH_AUTHORITY`/`VITE_OIDC_AUTHORITY`), `AZURE_CLIENT_ID`, `KEYVAULT_VAULT_URI`, the app keys, the live `AZURE_CLIENT_SECRET`, **+ POC.DAB** (`DAB_ENVIRONMENT`, `FABRIC_CONN_STRING`). | no (gitignored) |
| `.env.example` | committed template for both | **yes** |

Bootstrap: `cp .env.example .env.dev` (DEV defaults work as-is). For PROD, copy it to `.env.prod`
and switch to the `# PROD:` values + fill the prod-only ids + secret.

Naming: variables are **unprefixed and identical** across both files — the file you load
(`.env.dev` / `.env.prod`) decides the environment, not a `DEV_`/`PROD_` prefix.

## Run (from this root folder)

```powershell
# DEV — full stack, started in dependency order (OpenBAO -> Keycloak -> backend/frontend):
.\dev-up.ps1            # add -WithDab to also launch POC.DAB on the host (`dab start`)
.\dev-down.ps1          # stop the docker stacks

# Each sub-repo's compose can also run standalone (pass .env.dev):
docker compose -f POC.KeyCloak/docker-compose.yml --env-file .env.dev up
docker compose -f POC.OpenBao/docker-compose.yml  --env-file .env.dev up
docker compose -f POC.Authentication/infra/dev/docker-compose.yml --env-file .env.dev up --build  # app ONLY

# PROD-from-local — real Entra + Key Vault (needs Azure). Self-contained in .env.prod.
docker compose -f POC.Authentication/infra/prod/docker-compose.yml --env-file .env.prod up --build
```

`POC.Authentication/infra/dev` no longer starts Keycloak/OpenBAO (the umbrella was removed) — it's
app-only; **`dev-up.ps1` starts the stores first**, in order. Both app stacks publish the same host
ports (5173 / 5000) — run **one at a time**. DEV e2e: `cd POC.Authentication/frontend && npx
playwright test` against the running DEV stack.

> The frontend's own `frontend/.env.example` is unrelated — it documents Vite's local
> `npm run dev` (`.env.development`/`.env.production`), not the docker stacks.

## Security note

`AZURE_CLIENT_SECRET` was previously in `infra/prod/.env` in plaintext. It now lives only in
`.env.prod`. **Rotate it in Entra** and treat the old value as compromised.
