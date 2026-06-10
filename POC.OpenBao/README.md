# POC.OpenBao — DEV secret store

OpenBAO (dev mode, KV v2) plus a one-shot seed that writes the secret the backend fail-fasts
on. Acts as the local DEV stand-in for Azure Key Vault. Moved out of
`POC.Authentication/infra/dev` on 2026-06-08 into its own project.

```
POC.OpenBao/
  docker-compose.yml    # openbao (dev mode) + openbao-seed (one-shot)
  seed.sh               # writes Message--DisplayString to secret/poc
  .gitattributes        # force LF on *.sh
```

Env vars (`BAO_TOKEN`, `OPENBAO_PORT`) come from the **root `.env.dev`** (`POC/.env.dev`) — see
[../.env.example](../.env.example). There is no per-project `.env` anymore.

## Run

```sh
# Standalone (run from the POC root so --env-file resolves):
docker compose -f POC.OpenBao/docker-compose.yml --env-file .env.dev up

# As part of the full DEV stack — the root dev-up.ps1 starts this FIRST (OpenBAO -> Keycloak -> app),
# passing --env-file .env.dev.
```

| What | Value |
|---|---|
| URL | http://localhost:${OPENBAO_PORT} (default 8200; dev mode, KV v2 at `secret/`) |
| Root token | `BAO_TOKEN` from the root `.env.dev` (DEV-only placeholder) |
| Seeded path | `secret/poc` → `Message--DisplayString` |

## `BAO_TOKEN` — single source, no drift

The backend authenticates to this store with the **same** `BAO_TOKEN`. It now lives in exactly
**one** place — the root `POC/.env.dev` — read by both the store and the backend, so the two can never
fall out of sync.

## Seed values (DEV-only, non-secret)

`seed.sh` writes `Message--DisplayString` under the Key Vault `--` naming
(the backend's `OpenBaoSecretStoreReader` maps `--` → `:` so config keys match PROD). The value is
a non-secret DEV placeholder, intentionally **non-empty** — the backend rejects empty secrets and
refuses to start.

## Why these settings (do not "fix" them)

- **Healthcheck uses `127.0.0.1`, not `localhost`** — the dev listener binds `0.0.0.0` (IPv4-any)
  while `localhost` resolves `::1` first in this image → connection refused.
- **Seed entrypoint strips CR** (`sed 's/\r$//'`) so a CRLF working copy of `seed.sh` on Windows
  still runs; `.gitattributes` also forces LF.
