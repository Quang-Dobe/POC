#!/bin/sh
set -e

export BAO_ADDR="${BAO_ADDR:-http://openbao:8200}"
export VAULT_ADDR="$BAO_ADDR"
export BAO_TOKEN="${BAO_TOKEN:?BAO_TOKEN must be set (from infra/.env)}"
export VAULT_TOKEN="$BAO_TOKEN"

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

if ! command -v openssl >/dev/null 2>&1; then
  apk add --no-cache openssl >/dev/null 2>&1 || {
    echo "seed: openssl is required to generate the DEV signing key but is unavailable." >&2
    exit 1
  }
fi
openssl genpkey -algorithm RSA -pkeyopt rsa_keygen_bits:2048 -out /tmp/idp-signing.key 2>/dev/null
echo "seed: generated a DEV RSA-2048 signing key (not persisted to the repo)."

DP_MASTER_KEY="$(openssl rand -hex 32)"

bao kv put secret/poc \
  "Message--DisplayString=Hello from OpenBAO (DEV)" \
  "IdpSimulator--SigningKeyPem=@/tmp/idp-signing.key" \
  "DataProtection--MasterKey=${DP_MASTER_KEY}" \
  "Keycloak--AdminClientSecret=poc-admin-cli-dev-secret" \
  "Invite--DefaultPassword=Test1234!"

rm -f /tmp/idp-signing.key
echo "seed: wrote Message--DisplayString + IdpSimulator--SigningKeyPem + DataProtection--MasterKey + Keycloak--AdminClientSecret + Invite--DefaultPassword to secret/poc."
