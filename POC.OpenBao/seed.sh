#!/bin/sh
# OpenBAO DEV seed (secrets-dual-provider §"Naming convention", analyzed.md R4).
# Runs as a one-shot compose service against the dev-mode openbao server. Writes the logical
# secret the backend fail-fasts on into KV v2 at secret/poc, under the Key Vault "--" naming
# (Message--DisplayString) — the OpenBaoSecretStoreReader reads secret/data/poc
# and maps "--" -> ":" so config keys (Message:DisplayString) match PROD.
#
# The values below are NON-SECRET DEV placeholders defined at seed time (NOT committed real
# secrets). They are intentionally NON-EMPTY: SecretStoreConfiguration rejects empty values and
# refuses to start, so empty placeholders would fail the backend boot.
#
# Auth to OpenBAO uses the dev root token via $BAO_TOKEN (compose interpolation from the
# untracked infra/.env). The token is the single secret-via-env exception (secrets-dual-provider).
set -e

# Talk to the openbao service over the compose network. The dev server listens on :8200.
export BAO_ADDR="${BAO_ADDR:-http://openbao:8200}"
export VAULT_ADDR="$BAO_ADDR"          # VaultSharp/Vault tooling alias; harmless duplicate.
export BAO_TOKEN="${BAO_TOKEN:?BAO_TOKEN must be set (from infra/.env)}"
export VAULT_TOKEN="$BAO_TOKEN"

# Wait for the dev server to report healthy before writing (depends_on covers process start,
# not API readiness). sys/health returns 200 on a healthy unsealed/initialized dev server.
echo "seed: waiting for OpenBAO at ${BAO_ADDR} ..."
i=0
until wget -q -O /dev/null "${BAO_ADDR}/v1/sys/health"; do
  i=$((i + 1))
  if [ "$i" -ge 30 ]; then
    echo "seed: OpenBAO not healthy after $i attempts — aborting." >&2
    exit 1
  fi
  sleep 1
done
echo "seed: OpenBAO is up."

# KV v2 is auto-mounted at secret/ in dev mode.
# Placeholder value — DEV only, non-secret, non-empty.
bao kv put secret/poc \
  "Message--DisplayString=Hello from OpenBAO (DEV)"

echo "seed: wrote Message--DisplayString to secret/poc."
