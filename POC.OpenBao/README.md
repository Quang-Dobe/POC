# POC.OpenBao — DEV secret store

OpenBAO (dev mode, KV v2) plus a one-shot seed that writes the secrets the backend fail-fasts
on. Acts as the local DEV stand-in for Azure Key Vault. Moved out of
`infra/dev` on 2026-06-08 into its own project.

```
POC.OpenBao/
  docker-compose.yml    # openbao (dev mode) + openbao-seed (one-shot)
  seed.sh               # writes Message--DisplayString + IdpSimulator--SigningKeyPem to secret/poc
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
| Seeded path | `secret/poc` → `Message--DisplayString`, `IdpSimulator--SigningKeyPem` |

## `BAO_TOKEN` — single source, no drift

The backend authenticates to this store with the **same** `BAO_TOKEN`. It now lives in exactly
**one** place — the root `POC/.env.dev` — read by both the store and the backend, so the two can never
fall out of sync.

## Seed values (DEV-only)

`seed.sh` writes **two** secrets to `secret/poc` in a single `kv put`, both under the Key Vault `--`
naming (the backend's `OpenBaoSecretStoreReader` maps `--` → `:` so config keys match PROD):

- **`Message--DisplayString`** → `Message:DisplayString`. A non-secret DEV placeholder, intentionally
  **non-empty** — the backend rejects empty secrets and refuses to start.
- **`IdpSimulator--SigningKeyPem`** → `IdpSimulator:SigningKeyPem`. The RSA-2048 PKCS#8 private key the
  BFF (BE IDP-Simulator) uses to sign its minted RS256 downstream tokens. `IdpSimulator:SigningKeyPem`
  is in the backend's `RequiredKeys`, so the backend refuses to start without it. `seed.sh` **generates
  a fresh key at seed time** (`openssl genpkey`) — it is **never committed to the repo**, the same
  no-PEM-in-repo posture as the test suite. The seed container needs `openssl`; `seed.sh` runs
  `apk add --no-cache openssl` if it is missing.

PROD does not use OpenBAO — it reads the same logical secrets from **Azure Key Vault**, where the signing
key PEM lives under the same `IdpSimulator--SigningKeyPem` name.

## Why these settings (do not "fix" them)

- **Healthcheck uses `127.0.0.1`, not `localhost`** — the dev listener binds `0.0.0.0` (IPv4-any)
  while `localhost` resolves `::1` first in this image → connection refused.
- **Seed entrypoint strips CR** (`sed 's/\r$//'`) so a CRLF working copy of `seed.sh` on Windows
  still runs; `.gitattributes` also forces LF.
