# Nexora Hub

Vollständige Software-Lizenzierungs-Plattform auf **.NET 10** mit PostgreSQL,
Blazor-Web-Admin und nativer WPF-Desktop-App.

## Architektur

- **Nexora.Shared** – Modelle, Enums, DTOs (von API, Web und Desktop genutzt)
- **Nexora.Api** – ASP.NET Core Web API: Identity + JWT, EF Core, Lizenz-/Kontingentlogik, LiteLLM-Proxy
- **Nexora.Web** – Blazor-Server-Admin-Dashboard
- **Nexora.Desktop** – native WPF-Chat-App „Nexora AI" (kein WebView), MVVM
- **tests/Nexora.Api.Tests** – Integrationstests (isolierte SQLite-Test-DB, Fake-LiteLLM)

```
Client (WPF) ──JWT──▶ Nexora.Api ──API-Key──▶ LiteLLM
Admin (Blazor) ─JWT─▶ Nexora.Api ──▶ PostgreSQL (Schema "nexora")
```

## Datenbank

- Es wird **ausschließlich** das Schema `nexora` verwaltet. Das fremde
  Wharf-Schema `public` wird nie berührt.
- Verbindung/Secrets kommen aus `.env` (nicht eingecheckt), siehe `.env.example`.
- Migrationen (alle angewendet): `InitialCreate`, `NexoraSchema2`,
  `NexoraLicensingPlatform`.

## Funktionen

- **Lizenzierung**: zwei Pfade – (1) Einlösen kryptografisch sicherer Schlüssel,
  (2) direkte Admin-Zuweisung. Status: Active/Pending/Expired/Suspended/Revoked.
- **Schlüssel** werden nur als SHA-256-Hash gespeichert; Klartext erscheint genau einmal.
- **Serverseitige Limits**: Nachrichten/Tag+Monat, Tokens (In/Out/Total) pro Tag,
  max. Tokens pro Anfrage, erlaubte Modelle, max. Geräte. Parallelsicher reserviert
  (Npgsql `pg_advisory_xact_lock`).
- **Auth**: JWT (Access + rotierender Refresh), einmaliger Admin-Bootstrap,
  Registrierung vergibt nie Adminrechte, Brute-Force-Schutz.
- **LiteLLM-Schlüssel** verbleibt serverseitig; Clients sprechen nur mit der API.
- **Legacy**: `GET api/v1/apps` bleibt funktionsfähig.

## Schnellstart

```powershell
# 1) Konfiguration
Copy-Item .env.example .env   # Werte eintragen (DB, JWT-Key, optional LiteLLM)

# 2) Lösung bauen
dotnet build NexoraHub.slnx

# 3) API (liest .env)
dotnet run --project src\Nexora.Api --urls http://localhost:5199

# 4) Web-Admin (ApiBaseUrl = http://localhost:5199)
dotnet run --project src\Nexora.Web

# 5) Desktop-App
dotnet run --project src\Nexora.Desktop
```

Erst-Admin: über die Setup-Seite bzw. `POST api/v1/admin/setup` (nur solange
noch kein Admin existiert).

## Tests

```powershell
dotnet test tests\Nexora.Api.Tests\Nexora.Api.Tests.csproj
```

Die Tests laufen gegen eine isolierte SQLite-Datei und niemals gegen Wharf;
der LiteLLM-Dienst ist durch einen Fake ersetzt. Aktueller Stand: 20/20 bestanden
(16 geforderte Prüfungen + 4 Smoke-/Schema-Tests). Details: siehe `REPORT.md`.

## Release-Publish (Windows)

```powershell
dotnet publish src\Nexora.Desktop\Nexora.Desktop.csproj -c Release -r win-x64 `
  --self-contained true -o publish\NexoraAI-win-x64
```

Ergebnis: `publish\NexoraAI-win-x64\NexoraAI.exe`.

## Docker (Lizenzserver + Admin-Panel)

API und Admin-Panel lassen sich per Docker Compose betreiben (die Windows-App
bleibt ein separates natives Programm):

```bash
cp .env.example .env && chmod 600 .env   # Werte eintragen (siehe DEPLOYMENT.md)
docker compose build
docker compose run --rm nexora-api --migrate   # kontrollierte Migrationen (Schema "nexora")
docker compose up -d
```

- Externe PostgreSQL-Instanz (Schema `nexora`), **kein** zweiter DB-Server.
- Optionale Overrides: `docker-compose.local-db.yml` (lokale Test-DB),
  `docker-compose.litellm.yml` (lokale LiteLLM-Instanz).
- Vollständige Anleitung inkl. HTTPS/Reverse Proxy, Backups und Rollback:
  **`DEPLOYMENT.md`**.

## Hinweis zu LiteLLM

Ohne konfigurierte `NEXORA_LITELLM_URL`/`NEXORA_LITELLM_KEY` antwortet
`POST api/v1/chat/send` bewusst mit **503**; alle Lizenz-, Limit- und
Auth-Flows funktionieren unabhängig davon.
