$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

if (-not (Test-Path ".env.dev")) {
  throw ".env.dev not found. Run: Copy-Item .env.example .env.dev"
}

# --- TLS dev certs (idempotent) -------------------------------------------
# Trusted .NET dev cert: PEM for BFF/FE, PKCS12 (.pfx) for Keycloak HTTPS.
dotnet dev-certs https --trust | Out-Null
if ($LASTEXITCODE -ne 0) { throw "dotnet dev-certs --trust failed." }

New-Item -ItemType Directory -Force "infra\dev\tls\certs" | Out-Null
if (-not (Test-Path "infra\dev\tls\certs\dev-tls.crt")) {
  Write-Host "==> Exporting BFF/FE dev cert (PEM)" -ForegroundColor Cyan
  dotnet dev-certs https -ep "infra\dev\tls\certs\dev-tls.crt" --format pem --no-password
  if ($LASTEXITCODE -ne 0) { throw "dotnet dev-certs PEM export failed." }
}

# Docker creates the bind-mount target as an empty dir if the .pfx is missing -> Keycloak dies "Is a directory".
if (Test-Path -LiteralPath "POC.KeyCloak\certs\dev-cert.pfx" -PathType Container) {
  Remove-Item -LiteralPath "POC.KeyCloak\certs\dev-cert.pfx" -Recurse -Force
}
if (-not (Test-Path -LiteralPath "POC.KeyCloak\certs\dev-cert.pfx" -PathType Leaf)) {
  Write-Host "==> Exporting Keycloak dev cert (PKCS12)" -ForegroundColor Cyan
  dotnet dev-certs https -ep "POC.KeyCloak\certs\dev-cert.pfx" -p changeit
  if ($LASTEXITCODE -ne 0) { throw "dotnet dev-certs pfx export failed." }
}

# --- Stacks: OpenBao -> Keycloak -> FE+BFF -> Agent -> DAB ------------------
Write-Host "==> OpenBAO (secret store)  :8200" -ForegroundColor Cyan
docker compose -f "POC.OpenBao\docker-compose.yml" --env-file ".env.dev" up -d --wait
if ($LASTEXITCODE -ne 0) { throw "OpenBAO failed to start." }

Write-Host "==> Keycloak (identity)     :8080 https" -ForegroundColor Cyan
docker compose -f "POC.KeyCloak\docker-compose.yml" --env-file ".env.dev" up -d --wait
if ($LASTEXITCODE -ne 0) { throw "Keycloak failed to start." }

Write-Host "==> Backend + Frontend (app)" -ForegroundColor Cyan
docker compose -f "infra\dev\docker-compose.yml" --env-file ".env.dev" up -d --wait --build
if ($LASTEXITCODE -ne 0) { throw "Backend + Frontend failed to start." }

Write-Host "==> Agent Gateway           :8082" -ForegroundColor Cyan
docker compose -f "POC.Agent\docker-compose.yml" --env-file ".env.dev" up -d --wait --build
if ($LASTEXITCODE -ne 0) { throw "Agent Gateway failed to start." }

Write-Host ""
Write-Host "DEV docker stack is UP:" -ForegroundColor Green
Write-Host "  Frontend  https://localhost:5173"
Write-Host "  Backend   https://localhost:5000"
Write-Host "  Keycloak  https://localhost:8080  (realm poc, admin/admin)"
Write-Host "  OpenBAO   http://localhost:8200"
Write-Host "  Agent GW  http://localhost:8082"

# --- DAB (host process - interactive Fabric auth opens a browser; blocks) ---
Write-Host ""
Write-Host "==> POC.DAB (DEV, host process)" -ForegroundColor Cyan
Get-Content ".env.dev" | ForEach-Object {
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
