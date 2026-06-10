<#
  prod-down.ps1 — stop the local PROD docker stack (Agent -> app).
  PROD has no local Keycloak/OpenBAO (Azure AD + Key Vault), so only app + Agent are stopped here.
  POC.DAB PROD is a host process (`dab start`) — stop it with Ctrl+C in its own window.
  Pass -Volumes to also wipe volumes.
#>
param([switch]$Volumes)

$ErrorActionPreference = "Continue"
$root = $PSScriptRoot
$envFile = Join-Path $root ".env.prod"
$envArgs = (Test-Path $envFile) ? @("--env-file", $envFile) : @()
$downArgs = $Volumes ? @("down", "-v") : @("down")

foreach ($f in @(
  "POC.Agent\docker-compose.yml",
  "POC.Authentication\infra\prod\docker-compose.yml"
)) {
  Write-Host "==> down $f" -ForegroundColor Cyan
  docker compose -f (Join-Path $root $f) @envArgs @downArgs
}
Write-Host "PROD docker stack is DOWN." -ForegroundColor Green
