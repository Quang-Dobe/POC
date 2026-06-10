# PROD image: official DAB engine + base and Production configs baked in.
# Secrets are NEVER baked in — provide .env at runtime (docker run --env-file .env).
# DEV is not containerized (interactive Fabric auth needs a browser on the host).
FROM mcr.microsoft.com/azure-databases/data-api-builder:latest
COPY dab-config.json dab-config.Production.json /App/
# Base image listens on 5000 (ASPNETCORE_URLS=http://+:5000) — publish with -p <host>:5000
EXPOSE 5000
