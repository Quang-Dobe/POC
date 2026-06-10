# Backend image — multi-stage (infra-cicd §Images).
# Build stage: dotnet/sdk:8.0 restores + publishes the Api project (Release).
# Runtime stage: dotnet/aspnet:8.0, NON-ROOT user, ASPNETCORE_URLS=http://+:8080.
# Build context = ../backend (per Step G); paths below are relative to backend/.

# ---- build stage ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore against just the Api project file first (cache-friendly). The runtime image does not
# need the test project, so it is intentionally excluded from publish.
COPY src/Api/Api.csproj src/Api/
RUN dotnet restore src/Api/Api.csproj

# Copy the rest of the Api source and publish (Release, no app host needed — `dotnet Api.dll`).
COPY src/Api/ src/Api/
RUN dotnet publish src/Api/Api.csproj -c Release -o /app/publish --no-restore

# ---- runtime stage ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Kestrel binds inside the container on 8080 (infra-cicd); compose maps host 5000 -> 8080.
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish ./

# Run as the non-root user shipped in the aspnet:8.0 image (uid/gid 1654, name `app`).
USER app

ENTRYPOINT ["dotnet", "Api.dll"]
