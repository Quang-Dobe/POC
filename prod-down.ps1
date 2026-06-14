$ErrorActionPreference = "Continue"
Set-Location $PSScriptRoot

# Reverse of prod-up: Agent -> FE+BFF.
# (DAB is a host process, no container to bring down.)
Write-Host "==> down Agent Gateway" -ForegroundColor Cyan
docker compose -f "POC.Agent\docker-compose.yml" --env-file ".env.prod" down

Write-Host "==> down Backend + Frontend" -ForegroundColor Cyan
docker compose -f "infra\prod\docker-compose.yml" --env-file ".env.prod" down

Write-Host "PROD docker stack is DOWN." -ForegroundColor Green
