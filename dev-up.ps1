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

$certDir  = Join-Path $root "infra\dev\tls\certs"
$certFile = Join-Path $certDir "dev-tls.crt"
$keyFile  = Join-Path $certDir "dev-tls.key"
New-Item -ItemType Directory -Force $certDir | Out-Null

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

function Start-Stack([string]$label, [string]$composeRel, [string[]]$extra) {
  Write-Host "==> $label" -ForegroundColor Cyan
  Compose $composeRel (@("up", "-d", "--wait") + $extra)
  if ($LASTEXITCODE -ne 0) { throw "$label failed to start (exit $LASTEXITCODE)." }
}
Start-Stack "Keycloak (identity)     :8080 https" "POC.KeyCloak\docker-compose.yml" @()
Start-Stack "Backend + Frontend (app)"            "infra\dev\docker-compose.yml" @("--build")

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
  if (-not [Environment]::GetEnvironmentVariable('ASPNETCORE_URLS', 'Process')) {
    [Environment]::SetEnvironmentVariable('ASPNETCORE_URLS', 'http://0.0.0.0:8000', 'Process')
  }
  Push-Location (Join-Path $root "POC.DAB")
  try { dab start } finally { Pop-Location }
}
