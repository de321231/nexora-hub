# ============================================================================
# Nexora API (Lizenzserver) – mehrstufiger Build, nicht privilegierter Nutzer
# Build-Kontext: Repository-Root (da Nexora.Shared referenziert wird)
# ============================================================================

# ---- Stufe 1: Build mit SDK ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Nur Projektdateien kopieren für effizienten, cache-fähigen Restore
COPY src/Nexora.Shared/Nexora.Shared.csproj src/Nexora.Shared/
COPY src/Nexora.Api/Nexora.Api.csproj src/Nexora.Api/
RUN dotnet restore src/Nexora.Api/Nexora.Api.csproj

# Quellcode kopieren und Release-Publikation erzeugen
COPY src/Nexora.Shared/ src/Nexora.Shared/
COPY src/Nexora.Api/ src/Nexora.Api/
RUN dotnet publish src/Nexora.Api/Nexora.Api.csproj \
    -c Release --no-restore -o /app/publish -p:UseAppHost=false

# ---- Stufe 2: Laufzeit-Image (ASP.NET Core, Debian-basiert) ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

# curl wird ausschließlich für den Docker-Healthcheck benötigt
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_HTTP_PORTS=8080

# Dateien gehören dem nicht privilegierten Benutzer "app" (UID 1654)
COPY --from=build --chown=app:app /app/publish ./

USER app
EXPOSE 8080

# Alle umgebungsabhängigen Einstellungen kommen aus Umgebungsvariablen
# (NEXORA_DB, NEXORA_JWT_KEY, NEXORA_LITELLM_* …), gesetzt durch docker compose.
ENTRYPOINT ["dotnet", "Nexora.Api.dll"]