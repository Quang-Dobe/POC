# POC.OpenBao — DEV secret store

OpenBAO (HashiCorp Vault fork) running in **dev mode** with **KV v2**, plus a one-shot seed
container that writes the secrets the BFF fail-fasts on at boot. This is the local DEV stand-in
for Azure Key Vault (used in PROD).

```
POC.OpenBao/
  docker-compose.yml    # openbao (dev mode) + openbao-seed (one-shot)
  seed.sh               # writes the secret/poc keys (see below)
  .gitattributes        # force LF on *.sh
```

Env vars (`BAO_TOKEN`, `OPENBAO_PORT`) come from the **root `.env.dev`** (`POC/.env.dev`) — see
[../.env.example](../.env.example).

## Run

```sh
# Run from the POC root so --env-file resolves:
docker compose -f POC.OpenBao/docker-compose.yml --env-file .env.dev up
```

The compose file starts two services:

| Service | Role |
|---|---|
| `openbao` | dev-mode server, root token = `BAO_TOKEN`, listens on `0.0.0.0:8200` inside the container |
| `openbao-seed` | one-shot; waits for `openbao` to be healthy, then runs `seed.sh` and exits |

| What | Value |
|---|---|
| URL | `http://localhost:${OPENBAO_PORT}` (default `8200`; dev mode, KV v2 mounted at `secret/`) |
| Root token | `BAO_TOKEN` from the root `.env.dev` (DEV-only placeholder) |
| Seeded path | `secret/poc` (KV v2 → read at `secret/data/poc`) |

## What `seed.sh` provisions

`seed.sh` writes **four** keys to `secret/poc` in a single `bao kv put` (one put replaces the whole
object, so all keys must be written together). Keys use the Azure Key Vault `--` naming so the BFF's
key mapping (`--` → `:`) yields the same config keys as PROD:

| Store key (`secret/poc`) | Config key | Value (DEV) |
|---|---|---|
| `Message--DisplayString` | `Message:DisplayString` | Non-secret placeholder string (`Hello from OpenBAO (DEV)`) |
| `IdpSimulator--SigningKeyPem` | `IdpSimulator:SigningKeyPem` | Fresh RSA-2048 PKCS#8 private key, generated at seed time via `openssl genpkey` |
| `DataProtection--MasterKey` | `DataProtection:MasterKey` | Fresh 64-hex value via `openssl rand -hex 32` |
| `Keycloak--AdminClientSecret` | `Keycloak:AdminClientSecret` | `poc-admin-cli-dev-secret` (mirrors the DEV realm export) |

All values are **DEV-only**. The RSA key and Data Protection master key are **generated fresh at seed
time and never committed** to the repo. The seed container installs `openssl` via `apk add` if it is
missing.

## How the BFF reads these secrets

The BFF reads this store at startup through `Poc.Bff.Infrastructure.Secrets.OpenBaoSecretStoreReader`:

1. Authenticates with token auth using the `BAO_TOKEN` env var.
2. Reads KV v2 at mount `secret`, path `poc` (i.e. `secret/data/poc`).
3. Maps each store key `--` → `:` (`SecretKeyMapping`) and loads them into configuration.

The server address comes from config `Vault:Address`. `BAO_TOKEN` is shared by both this store and the
BFF and lives in exactly **one** place — the root `POC/.env.dev` — so the two cannot drift.

The BFF refuses to start unless these **required** config keys are present and non-empty
(`SecretStoreConfiguration.RequiredKeys`):

- `Message:DisplayString`
- `IdpSimulator:SigningKeyPem`
- `DataProtection:MasterKey`

(`Keycloak:AdminClientSecret` is seeded but is not in the required set.)

## PROD

PROD does not use OpenBAO. The BFF reads the same logical secrets from **Azure Key Vault**
(`KeyVaultSecretStoreReader`, selected when `ENV=PROD`), where they live under the same `--` names.

## Why these settings (do not "fix" them)

- **Healthcheck uses `127.0.0.1`, not `localhost`** — the dev listener binds `0.0.0.0` (IPv4-any)
  while `localhost` resolves `::1` first in this image → connection refused.
- **Seed entrypoint strips CR** (`sed 's/\r$//'`) so a CRLF working copy of `seed.sh` on Windows
  still runs; `.gitattributes` also forces LF.
