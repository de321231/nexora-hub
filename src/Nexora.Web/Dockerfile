# ============================================================================
# Nexora Web (Admin-Panel, Blazor Server) – mehrstufiger Build, nicht privilegierter Nutzer
# Build-Kontext: Repository-Root (da Nexora.Shared referenziert wird)
# ============================================================================

# ---- Stufe 1: Build mit SDK ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Nur Projektdateien kopieren für effizienten, cache-fähigen Restore
COPY src/Nexora.Shared/Nexora.Shared.csproj src/Nexora.Shared/
COPY src/Nexora.Web/Nexora.Web.csproj src/Nexora.Web/
RUN dotnet restore src/Nexora.Web/Nexora.Web.csproj

# Quellcode kopieren und Release-Publikation erzeugen
COPY src/Nexora.Shared/ src/Nexora.Shared/
COPY src/Nexora.Web/ src/Nexora.Web/
RUN dotnet publish src/Nexora.Web/Nexora.Web.csproj \
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

# ApiBaseUrl und weitere Einstellungen kommen aus Umgebungsvariablen,
# gesetzt durch docker compose (intern: http://nexora-api:8080).
ENTRYPOINT ["dotnet", "Nexora.Web.dll"]