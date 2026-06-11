<#
  dev-up.ps1 — start the local DEV stack in DEPENDENCY ORDER, each from its own sub-repo compose:
      OpenBAO (secret store) -> Keycloak (identity) -> Backend + Frontend (app) -> [Agent Gateway] -> [POC.DAB DEV]

  The infra/dev umbrella was removed (2026-06-08): `docker compose` inside infra/dev now
  starts ONLY the app (BE/FE). This script is the single place that brings the whole DEV stack up in order.

  Usage:
    .\dev-up.ps1            # docker stacks only (OpenBAO + Keycloak + BE/FE)
    .\dev-up.ps1 -WithDab   # also launch POC.DAB on the host (`dab start`; needs Fabric + a browser)
    .\dev-up.ps1 -WithAgent   # also start Agent Gateway container (needs Azure creds in .env.dev)

  Prereqs (one-time): copy .env.example -> .env.dev (root) and generate the Keycloak dev cert:
    Copy-Item .env.example .env.dev
    dotnet dev-certs https -ep POC.KeyCloak\certs\dev-cert.pfx -p changeit
#>
param([switch]$WithDab, [switch]$WithAgent)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$envFile = Join-Path $root ".env.dev"
if (-not (Test-Path $envFile)) {
  throw ".env.dev not found at $root. Run: Copy-Item .env.example .env.dev"
}

function Compose([string]$composeRel, [string[]]$cmdArgs) {
  docker compose -f (Join-Path $root $composeRel) --env-file $envFile @cmdArgs
}

# --- TLS cert: keep the mounted PEM in lockstep with the TRUSTED dev cert ----------------
# DAB runs on the host and fetches the BFF's OIDC metadata over https://localhost:5000,
# validating the served cert against the host trust store. If the mounted PEM drifts from
# the trusted dev cert (e.g. the dev cert was regenerated), DAB's metadata fetch fails ->
# no JWKS -> every token is rejected 401. So compare thumbprints every run, not just presence.
$certDir  = Join-Path $root "infra\dev\tls\certs"
$certFile = Join-Path $certDir "dev-tls.crt"
$keyFile  = Join-Path $certDir "dev-tls.key"
New-Item -ItemType Directory -Force $certDir | Out-Null

# Idempotent: a no-op (no UAC prompt) when the dev cert is already trusted.
dotnet dev-certs https --trust | Out-Null
if ($LASTEXITCODE -ne 0) { throw "dotnet dev-certs --trust failed." }

$check        = dotnet dev-certs https --check --trust 2>&1 | Out-String
$trustedThumb = ([regex]::Match($check, '([0-9A-Fa-f]{40})')).Value

$mountedThumb = ''
if ((Test-Path $certFile) -and (Test-Path $keyFile)) {
  try {
    $mountedThumb = [System.Security.Cryptography.X509Certificates.X509Certificate2]::CreateFromPem((Get-Content $certFile -Raw)).Thumbprint
  } catch { $mountedThumb = '' }
}

if ($mountedThumb -and ($mountedThumb -eq $trustedThumb)) {
  Write-Host "==> TLS cert in sync with trusted dev cert ($trustedThumb)" -ForegroundColor DarkGray
} else {
  Write-Host "==> TLS cert missing/stale (mounted=$mountedThumb trusted=$trustedThumb) — re-exporting" -ForegroundColor Cyan
  dotnet dev-certs https -ep $certFile --format pem --no-password
  if ($LASTEXITCODE -ne 0) { throw "dotnet dev-certs export failed." }
  Write-Host "    Re-exported trusted cert to $certFile (recreate the BFF/FE containers to load it)" -ForegroundColor Green
}

# Order matters: the backend fail-fasts on an unreachable store and 401s without Keycloak's JWKS.

# --- OpenBAO -------------------------------------------------------------------------------------
# Has a one-shot `openbao-seed` that EXITS 0 — `up --wait` mis-reports that exit as a failure, so
# start detached and poll explicitly for openbao healthy + seed exited 0.
Write-Host "==> OpenBAO (secret store)  :8200" -ForegroundColor Cyan
Compose "POC.OpenBao\docker-compose.yml" @("up", "-d")
if ($LASTEXITCODE -ne 0) { throw "OpenBAO failed to start." }
$deadline = (Get-Date).AddSeconds(120)
while ($true) {
  $st = (Compose "POC.OpenBao\docker-compose.yml" @("ps", "-a", "--format", "{{.Service}}={{.Status}}")) -join " | "
  if ($st -match 'openbao-seed=Exited \([1-9]') { throw "openbao-seed failed: $st" }
  if (($st -match 'openbao=Up.*\(healthy\)') -and ($st -match 'openbao-seed=Exited \(0\)')) { break }
  if ((Get-Date) -gt $deadline) { throw "OpenBAO not ready in time: $st" }
  Start-Sleep -Seconds 3
}

# --- Keycloak + app ------------------------------------------------------------------------------
# No one-shot containers here, so `up --wait` is safe (waits for healthy / running).
function Start-Stack([string]$label, [string]$composeRel, [string[]]$extra) {
  Write-Host "==> $label" -ForegroundColor Cyan
  Compose $composeRel (@("up", "-d", "--wait") + $extra)
  if ($LASTEXITCODE -ne 0) { throw "$label failed to start (exit $LASTEXITCODE)." }
}
Start-Stack "Keycloak (identity)     :8080 https" "POC.KeyCloak\docker-compose.yml" @()
Start-Stack "Backend + Frontend (app)"            "infra\dev\docker-compose.yml" @("--build")

# --- Agent Gateway (optional) ------------------------------------------------------------------
if ($WithAgent -or ([Environment]::GetEnvironmentVariable("AGENT_GATEWAY_ENABLED", "Process") -eq "true")) {
  Start-Stack "Agent Gateway              :8082" "POC.Agent\docker-compose.yml" @("--build")
}

Write-Host ""
Write-Host "DEV docker stack is UP:" -ForegroundColor Green
Write-Host "  Frontend  https://localhost:5173"
Write-Host "  Backend   https://localhost:5000   (/ liveness, /api/message gated)"
Write-Host "  Keycloak  https://localhost:8080  (realm poc, admin/admin)"
Write-Host "  OpenBAO   http://localhost:8200"
if ($WithAgent -or ([Environment]::GetEnvironmentVariable("AGENT_GATEWAY_ENABLED", "Process") -eq "true")) {
  Write-Host "  Agent GW  http://localhost:8082  (POST /ask, GET /health)"
}
Write-Host "  FE tests: cd POC.FE; npm test"

if ($WithDab) {
  Write-Host ""
  Write-Host "==> POC.DAB (DEV, host process - interactive Fabric auth opens a browser)" -ForegroundColor Cyan
  Get-Content $envFile | ForEach-Object {
    $line = $_.Trim()
    if ($line -and -not $line.StartsWith('#') -and $line.Contains('=')) {
      $i = $line.IndexOf('=')
      $k = $line.Substring(0, $i).Trim()
      $v = $line.Substring($i + 1).Trim()
      $v = [regex]::Replace($v, '\$\{(\w+)\}', { param($m) [Environment]::GetEnvironmentVariable($m.Groups[1].Value, 'Process') })
      [Environment]::SetEnvironmentVariable($k, $v, 'Process')
    }
  }
  # Bind to 0.0.0.0 so Docker containers can reach DAB via host.docker.internal:8000.
  # DAB's own default (:5000) would collide with the backend's host :5000.
  if (-not [Environment]::GetEnvironmentVariable('ASPNETCORE_URLS', 'Process')) {
    [Environment]::SetEnvironmentVariable('ASPNETCORE_URLS', 'http://0.0.0.0:8000', 'Process')
  }
  Push-Location (Join-Path $root "POC.DAB")
  try { dab start } finally { Pop-Location }   # blocks; Ctrl+C to stop DAB (docker stays up)
}
