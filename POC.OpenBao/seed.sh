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

# The BE IDP-Simulator signs downstream tokens with an RSA-2048 private key it loads from the
# secret store (IdpSimulator:SigningKeyPem, in RequiredKeys — the backend refuses to start without
# it). Generate a fresh PKCS#8 key at seed time (DEV only, NEVER committed — same no-PEM-in-repo
# posture as the test suite) and write it alongside the message placeholder. openssl emits the
# exact PKCS#8 shape SigningKeyProvider pins to RSA.ImportFromPem.
if ! command -v openssl >/dev/null 2>&1; then
  apk add --no-cache openssl >/dev/null 2>&1 || {
    echo "seed: openssl is required to generate the DEV signing key but is unavailable." >&2
    exit 1
  }
fi
openssl genpkey -algorithm RSA -pkeyopt rsa_keygen_bits:2048 -out /tmp/idp-signing.key 2>/dev/null
echo "seed: generated a DEV RSA-2048 signing key (not persisted to the repo)."

# Task 2: DataProtection master STRING secret. RequiredKeys includes DataProtection:MasterKey, so the
# backend fail-fasts at boot without it. The AES-256-GCM key that encrypts the Data Protection key-ring
# before it lands in Redis is HKDF-derived from this string. Generate a fresh 64-hex DEV value (>=32
# chars) at seed time (DEV only, NEVER committed).
DP_MASTER_KEY="$(openssl rand -hex 32)"

# KV v2 is auto-mounted at secret/ in dev mode. ONE put writes ALL keys — each `put` REPLACES the whole
# secret/poc object, so a later/split put would clobber the PEM. Message--DisplayString is a non-secret
# DEV placeholder; the PEM is read from the temp file via @; Keycloak--AdminClientSecret (Task 3) is the
# poc-admin-cli DEV secret mirrored from realm-export.json so the Keycloak Admin REST invite flow works.
bao kv put secret/poc \
  "Message--DisplayString=Hello from OpenBAO (DEV)" \
  "IdpSimulator--SigningKeyPem=@/tmp/idp-signing.key" \
  "DataProtection--MasterKey=${DP_MASTER_KEY}" \
  "Keycloak--AdminClientSecret=poc-admin-cli-dev-secret"

rm -f /tmp/idp-signing.key
echo "seed: wrote Message--DisplayString + IdpSimulator--SigningKeyPem + DataProtection--MasterKey + Keycloak--AdminClientSecret to secret/poc."
