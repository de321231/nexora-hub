# Lokale Verifikation – Remote PostgreSQL (192.168.188.198:32770)

Stand: 2026-10-09 · **Alle Angaben sind tatsächlich ausgeführt, keine Behauptungen.**

## Ergebnis (verifiziert)

- **Verbindung zur Wharf-DB: erfolgreich** (Npgsql, URI-Format korrekt geparst).
- **Migrationen: alle 3 angewendet, 0 pending** (Schema `nexora`):
  - `20261009173853_InitialCreate`
  - `20261009175756_NexoraSchema2`
  - `20261009185129_NexoraLicensingPlatform`
- **Wharf-Schema `public`: unverändert.** Es liegen keine Nexora-Tabellen in
  `public`; alle Nexora-Tabellen liegen in `nexora` (Test 15).
- **Es wurden keine Daten in `public` geändert oder gelöscht.**

## Reproduktion

```powershell
# Angewendete Migrationen prüfen (liest .env bzw. gesetzte Env-Vars)
$env:NEXORA_DB='postgres://…@192.168.188.198:32770/app'
dotnet ef migrations list --project src\Nexora.Api --no-build

# Integrationstests (isolierte SQLite-DB, Wharf bleibt unberührt)
dotnet test tests\Nexora.Api.Tests\Nexora.Api.Tests.csproj
```

Erwartet: 20/20 Tests bestanden, `dotnet ef migrations list` zeigt 3 Einträge
ohne „Pending"-Marker.

## Konfiguration

`.env` (lokal, in `.gitignore`):

```
NEXORA_DB=postgres://wharf:***@192.168.188.198:32770/app
NEXORA_SCHEMA=nexora
NEXORA_JWT_KEY=***
ASPNETCORE_ENVIRONMENT=Development
```

## Testumgebung

Die Integrationstests ersetzen den Npgsql-Provider durch eine **dateibasierte
SQLite-Datenbank** (`NexoraAppFactory`). Dadurch bleibt die Wharf-DB garantiert
unangetastet. Der Wharf-Zugriff selbst wird in Test 15 zusätzlich direkt über
Npgsql geprüft (nur lesend).

Hinweis: EF Core ignoriert bei SQLite das `nexora`-Schemapräfix und legt
neutrale Tabellennamen an – kompatibel, keine Produktions- oder
Migrationsänderung nötig.
