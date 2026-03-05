# ── Etapa 1: Build ─────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copiar archivos de proyecto para restore (aprovechar cache de capas)
COPY POS.Domain/POS.Domain.csproj         POS.Domain/
COPY POS.Application/POS.Application.csproj POS.Application/
COPY POS.Infrastructure/POS.Infrastructure.csproj POS.Infrastructure/
COPY POS.Api/POS.Api.csproj               POS.Api/

RUN dotnet restore POS.Api/POS.Api.csproj

# Copiar código fuente y publicar
COPY POS.Domain/         POS.Domain/
COPY POS.Application/    POS.Application/
COPY POS.Infrastructure/ POS.Infrastructure/
COPY POS.Api/            POS.Api/

RUN dotnet publish POS.Api/POS.Api.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore

# ── Etapa 2: Runtime ──────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Copiar artefacto publicado
COPY --from=build /app/publish .

# Puerto de la aplicación
EXPOSE 5000

# Variables de entorno por defecto (sobreescribibles en docker-compose / K8s)
ENV ASPNETCORE_URLS=http://+:5000 \
    ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "POS.Api.dll"]
