<#
  dev-down.ps1 — stop the local DEV docker stack (reverse order: app -> Keycloak -> OpenBAO).
  POC.DAB DEV is a host process (`dab start`) — stop it with Ctrl+C in its own window.
  Pass -Volumes to also wipe volumes (Keycloak/OpenBAO dev state is ephemeral anyway).
#>
param([switch]$Volumes)

$ErrorActionPreference = "Continue"
$root = $PSScriptRoot
$envFile = Join-Path $root ".env.dev"
$envArgs = (Test-Path $envFile) ? @("--env-file", $envFile) : @()
$downArgs = $Volumes ? @("down", "-v") : @("down")

foreach ($f in @(
  "POC.Agent\docker-compose.yml",
  "POC.Authentication\infra\dev\docker-compose.yml",
  "POC.KeyCloak\docker-compose.yml",
  "POC.OpenBao\docker-compose.yml"
)) {
  Write-Host "==> down $f" -ForegroundColor Cyan
  docker compose -f (Join-Path $root $f) @envArgs @downArgs
}
Write-Host "DEV docker stack is DOWN." -ForegroundColor Green
