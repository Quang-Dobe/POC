FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY nuget.config ./
COPY src/Poc.Bff.Domain/Poc.Bff.Domain.csproj src/Poc.Bff.Domain/
COPY src/Poc.Bff.Application/Poc.Bff.Application.csproj src/Poc.Bff.Application/
COPY src/Poc.Bff.Infrastructure/Poc.Bff.Infrastructure.csproj src/Poc.Bff.Infrastructure/
COPY src/Poc.Bff.Api/Poc.Bff.Api.csproj src/Poc.Bff.Api/
RUN dotnet restore src/Poc.Bff.Api/Poc.Bff.Api.csproj

COPY src/ src/
RUN dotnet publish src/Poc.Bff.Api/Poc.Bff.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish ./

USER app

ENTRYPOINT ["dotnet", "Poc.Bff.Api.dll"]
