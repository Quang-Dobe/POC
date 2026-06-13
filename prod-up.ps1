param([switch]$WithDab, [switch]$WithAgent)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$envFile = Join-Path $root ".env.prod"
if (-not (Test-Path $envFile)) {
  throw ".env.prod not found at $root. Run: Copy-Item .env.example .env.prod"
}

function Compose([string]$composeRel, [string[]]$cmdArgs) {
  docker compose -f (Join-Path $root $composeRel) --env-file $envFile @cmdArgs
}

function Start-Stack([string]$label, [string]$composeRel, [string[]]$extra) {
  Write-Host "==> $label" -ForegroundColor Cyan
  Compose $composeRel (@("up", "-d", "--wait") + $extra)
  if ($LASTEXITCODE -ne 0) { throw "$label failed to start (exit $LASTEXITCODE)." }
}
Start-Stack "Backend + Frontend (app)" "infra\prod\docker-compose.yml" @("--build")

if ($WithAgent -or ([Environment]::GetEnvironmentVariable("AGENT_GATEWAY_ENABLED", "Process") -eq "true")) {
  Start-Stack "Agent Gateway              :8082" "POC.Agent\docker-compose.yml" @("--build")
}

Write-Host ""
Write-Host "PROD docker stack is UP:" -ForegroundColor Green
Write-Host "  Frontend  http://localhost:5173"
Write-Host "  Backend   http://localhost:5000   (/ liveness, /api/message gated)"
Write-Host "  Identity  Azure AD (login.microsoftonline.com)"
Write-Host "  Secrets   Azure Key Vault"
if ($WithAgent -or ([Environment]::GetEnvironmentVariable("AGENT_GATEWAY_ENABLED", "Process") -eq "true")) {
  Write-Host "  Agent GW  http://localhost:8082  (POST /ask, GET /health)"
}

if ($WithDab) {
  Write-Host ""
  Write-Host "==> POC.DAB (PROD, host process — interactive Fabric auth opens a browser)" -ForegroundColor Cyan
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
