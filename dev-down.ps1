$ErrorActionPreference = "Continue"
Set-Location $PSScriptRoot

# Reverse of dev-up: Agent -> FE+BFF -> Keycloak -> OpenBao.
# (DAB is a host process, no container to bring down.)
Write-Host "==> down Agent Gateway" -ForegroundColor Cyan
docker compose -f "POC.Agent\docker-compose.yml" --env-file ".env.dev" down

Write-Host "==> down Backend + Frontend" -ForegroundColor Cyan
docker compose -f "infra\dev\docker-compose.yml" --env-file ".env.dev" down

Write-Host "==> down Keycloak" -ForegroundColor Cyan
docker compose -f "POC.KeyCloak\docker-compose.yml" --env-file ".env.dev" down

Write-Host "==> down OpenBAO" -ForegroundColor Cyan
docker compose -f "POC.OpenBao\docker-compose.yml" --env-file ".env.dev" down

Write-Host "DEV docker stack is DOWN." -ForegroundColor Green
