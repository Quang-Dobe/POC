$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

if (-not (Test-Path ".env.prod")) {
  throw ".env.prod not found. Run: Copy-Item .env.example .env.prod"
}

# --- Dev TLS cert (self-signed) — generated if missing ---------------------
# Backend (Kestrel) and frontend (nginx) both mount infra/prod/certs.
# Cert is dev-only and gitignored, so generate it on any machine that lacks one.
$certDir = "infra\prod\certs"
$crt = Join-Path $certDir "dev-tls.crt"
$key = Join-Path $certDir "dev-tls.key"
if (-not (Test-Path $crt) -or -not (Test-Path $key)) {
  Write-Host "==> Generating self-signed dev TLS cert" -ForegroundColor Cyan
  $openssl = (Get-Command openssl -ErrorAction SilentlyContinue).Source
  if (-not $openssl) {
    $openssl = "C:\Program Files\Git\usr\bin\openssl.exe"
    if (-not (Test-Path $openssl)) { throw "openssl not found. Add it to PATH or install Git for Windows." }
  }
  New-Item -ItemType Directory -Force -Path $certDir | Out-Null
  & $openssl req -x509 -newkey rsa:2048 -nodes -keyout $key -out $crt -days 365 `
    -subj "/CN=localhost" -addext "subjectAltName=DNS:localhost,IP:127.0.0.1"
  if ($LASTEXITCODE -ne 0) { throw "openssl cert generation failed." }
}

# --- Stacks: FE+BFF -> Agent -> DAB ---------------------------------------
# Identity = Azure AD, secrets = Azure Key Vault (no OpenBao/Keycloak in prod).
Write-Host "==> Backend + Frontend (app)" -ForegroundColor Cyan
docker compose -f "infra\prod\docker-compose.yml" --env-file ".env.prod" up -d --wait --build
if ($LASTEXITCODE -ne 0) { throw "Backend + Frontend failed to start." }

Write-Host "==> Agent Gateway           :8082" -ForegroundColor Cyan
docker compose -f "POC.Agent\docker-compose.yml" --env-file ".env.prod" up -d --wait --build
if ($LASTEXITCODE -ne 0) { throw "Agent Gateway failed to start." }

Write-Host ""
Write-Host "PROD docker stack is UP:" -ForegroundColor Green
Write-Host "  Frontend  https://localhost:5173"
Write-Host "  Backend   https://localhost:5000"
Write-Host "  Identity  Azure AD (login.microsoftonline.com)"
Write-Host "  Secrets   Azure Key Vault"
Write-Host "  Agent GW  http://localhost:8082"

# --- DAB (host process - interactive Fabric auth opens a browser; blocks) ---
Write-Host ""
Write-Host "==> POC.DAB (PROD, host process)" -ForegroundColor Cyan
Get-Content ".env.prod" | ForEach-Object {
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
Set-Location "POC.DAB"
dab start
