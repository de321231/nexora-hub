# Nexora Hub – Abschluss-Report

Stand: 2026-10-09 · Arbeitsverzeichnis: `C:\Users\david\Desktop\NexoraHub`
Plattform: **.NET 10** · PostgreSQL (Wharf) · Blazor-Web-Admin · native WPF-App

---

## 1. Umgesetzte Funktionen

### Lizenzierung
- **Zwei Lizenzpfade**: (1) Einlösen kryptografisch sicherer Lizenzschlüssel, (2) direkte Zuweisung durch den Admin.
- **Statusmodell**: `Active`, `Pending`, `Expired`, `Suspended`, `Revoked` (zeitliche Auswertung zentral in `LicenseEvaluator`).
- **Schlüssel nur gehasht** (SHA-256 über normalisierten Schlüssel); Klartext erscheint **genau einmal** bei der Erstellung.
- **Diagramm**: `Product` (App) → `License` → `LicenseModel` (erlaubte KI-Modelle); `LicenseKey` → `UserLicense` → `Activation`/`Device`.

### Serverseitige Limit-Durchsetzung
- Nachrichten pro Tag/Monat, Eingabe-/Ausgabe-/Gesamt-Tokens pro Tag/Monat, **max. Tokens pro Anfrage**, erlaubte Modelle, **max. Geräte**.
- **Parallelsicherheit**: Kontingent wird vor der Anfrage reserviert und nach der Antwort abgeglichen (`QuotaService.ReserveAsync`/`ReconcileAsync`). Für Npgsql via `pg_advisory_xact_lock`, für Nicht-Npgsql (z. B. SQLite in Tests) über Transaktionsserialisierung.
- Parallel laufende Anfragen können das Limit **nicht** umgehen (durch Test 10 nachgewiesen).

### Authentifizierung & Sicherheit
- Native JWT-Authentifizierung (Access + rotierender Refresh-Token); Schemata explizit auf `JwtBearer` gesetzt.
- Erst-Admin ausschließlich über **einmaligen Bootstrap**; danach dauerhaft gesperrt. Registrierung vergibt **niemals** Adminrechte.
- Brute-Force-Schutz (Rate Limiting + Identity-Lockout).
- Der **LiteLLM-Schlüssel verlässt niemals den Server**; Clients sprechen nur mit der Nexora-API, die API spricht mit LiteLLM.
- CORS, Swagger (nur Development), Serilog-Logging.

### Admin-Web-Panel (Blazor Server)
- Seiten: Dashboard, Benutzer, Apps, Modelle, Lizenzen, Lizenzschlüssel, Geräte, Nutzung, Bootstrap-Setup, Login.
- JWT wird serverseitig in einem gescopten `TokenStore` gehalten, automatischer Refresh bei 401.
- Durchgängiges Design-System (`app.css`, dunkles Anthrazit + Violett).

### Native Windows-App „Nexora AI“ (WPF, kein WebView)
- MVVM mit `CommunityToolkit.Mvvm`; Sidebar-Shell mit Login- vs. App-Ansicht.
- Ansichten: Login, Chat (Markdown-Rendering), Lizenzen, Geräte, Einstellungen.
- Dienste: `NexoraApiClient` (401-Retry/Refresh), `SessionStore` + `SecureTokenStorage` (DPAPI, `session.dat`), `AppSettings` (API-URL + Geräte-GUID), `MarkdownRenderer`.
- Lade-/Fehlerzustände, konfigurierbare API-URL, dunkles Theme.

### Kompatibilität
- Legacy-Endpunkt `GET api/v1/apps` bleibt erhalten und funktioniert weiterhin.
- Es wird **ausschließlich** das Schema `nexora` verwaltet; `public` (Wharf) bleibt unberührt (durch Test 15 nachgewiesen).

---

## 2. Erstellte / geänderte Dateien (Auswahl)

**Server (`src/Nexora.Api`)**
- `Program.cs` (DI, JWT, Rate Limiting, CORS, Seeder-Start, `.env`-Laden)
- `Data/AppDbContext.cs`
- `Controllers/`: `AuthController`, `LicenseController`, `DeviceController`, `ChatController`, `AppController` (Legacy) sowie `Admin*` (Setup, Users, Apps, Models, Licenses, LicenseKeys, Usage, Dashboard, Devices)
- `Services/`: `TokenService`, `QuotaService`, `DeviceService`, `LiteLlmClient`, `LicenseEvaluator`, `LicenseKeyGenerator`, `DatabaseSeeder`, `DtoMapper`, `ClaimsExtensions`, `AuditService`
- `Options/AppOptions.cs`, `Migrations/20261009185129_NexoraLicensingPlatform*`

**Gemeinsam (`src/Nexora.Shared`)**
- `Models/*` (Product, License, LicenseKey, UserLicense, AiModel, LicenseModel, Activation, Device, UsagePeriod, UsageEvent, ChatConversation, ChatMessage, RefreshToken, AuditLog, ApplicationUser, Enums)
- `Dtos/Dtos.cs`

**Web-Admin (`src/Nexora.Web`)**
- `Components/Pages/**` (Dashboard, Users, Apps, Models, Licenses, LicenseKeys, Devices, Usage, Login, Setup, NotFound, AccessDenied)
- `Services/NexoraApiClient.cs`, `Services/TokenStore.cs`, `Components/Layout/*`, `wwwroot/app.css`

**Desktop (`src/Nexora.Desktop`)**
- `Services/`: `NexoraApiClient.cs`, `SessionStore.cs`, `SecureTokenStorage.cs`, `AppSettings.cs`, `MarkdownRenderer.cs`
- `ViewModels/`: `Main`, `Login`, `Chat`, `Licenses`, `Devices`, `Settings`
- `Views/`: `LoginView`, `ChatView`, `LicensesView`, `DevicesView`, `SettingsView`
- `MainWindow.xaml(.cs)`, `App.xaml(.cs)`, `Converters/Converters.cs`, `Theme/ThemeResources.xaml`, `app.manifest`

**Tests (`tests/Nexora.Api.Tests`)**
- `TestFactory.cs` (isolierte SQLite-Test-DB, Fake-LiteLLM, entschärfte Rate-Limits), `ApiTestHelpers.cs`, `TestData.cs`
- `AuthTests.cs`, `LicenseFlowTests.cs`, `QuotaTests.cs`, `SecurityTests.cs`, `InfrastructureTests.cs`, `BootSmokeTests.cs`
- `Nexora.Api.Tests.csproj` (FrameworkReference ASP.NET Core, `Microsoft.Data.Sqlite`, `Npgsql`)

**Konfiguration / Doku**
- `.env` (lokal, nicht eingecheckt), `.env.example`, `README.md`, `TEST-Lokal.md`, `REPORT.md`
- Docker: `docker-compose.yml`, `docker-compose.local-db.yml` (optional), `docker-compose.litellm.yml` (optional), `src/Nexora.Api/Dockerfile`, `src/Nexora.Web/Dockerfile`, `.dockerignore`, `deploy/litellm/config.yaml.example`, `DEPLOYMENT.md`
- Gelöscht: leere Template-Reste `src/Nexora.Api/Class1.cs`, `src/Nexora.Shared/Class1.cs`, veraltete `deploy/docker-compose.yml` und `deploy/api.Dockerfile` (referenzierten nicht existierende Dateien/Ports)

---

## 3. Ausgeführte Migrationen & Zielschema

Verbindung: `postgres://wharf:***@192.168.188.198:32770/app`
Verwaltetes Schema: **`nexora`** (History-Tabelle `nexora.__EFMigrationsHistory`).

`dotnet ef migrations list` gegen die Wharf-DB ergab (0 pending):

| Migration | Status |
|---|---|
| `20261009173853_InitialCreate` | angewendet |
| `20261009175756_NexoraSchema2` | angewendet |
| `20261009185129_NexoraLicensingPlatform` | angewendet |

> Hinweis: Migrationen wurden ausschließlich gegen das Schema `nexora` ausgeführt. Das Wharf-Schema `public` wurde nicht verändert (Test 15).

---

## 4. Tests & tatsächliche Ergebnisse

Ausgeführt mit:

```
dotnet test tests\Nexora.Api.Tests\Nexora.Api.Tests.csproj
```

**Ergebnis: 20 von 20 Tests bestanden** (Dauer ≈ 8,8 s). Davon die **16 geforderten Prüfungen** plus 4 Boot-/Schema-Smoke-Tests:

| # | Test | Prüft |
|---|---|---|
| 1 | `Auth_RegisterAndLogin_Work` | Registrierung + Login, falsches Passwort → 401 |
| 2 | `AuthGuards_RequireAuthentication` | Geschützte Endpunkte → 401 ohne Token |
| 3 | `AdminEndpoints_RejectNonAdmin_AndRegisterGrantsNoAdmin` | Admin-Endpunkte → 403; Registrierung vergibt keine Adminrolle |
| 4 | `Key_RedeemedExactlyOnce` | Schlüssel genau einmal einlösbar (auch für andere Benutzer gesperrt) |
| 5 | `License_DirectAssignment_VisibleToUser` | Direkte Zuweisung sichtbar in „meine Lizenzen" |
| 6 | `License_ExpiredOrSuspended_ChatRejected` | Abgelaufene/gesperrte Lizenz → Chat 403 |
| 7 | `Model_RestrictionEnforced` | Nicht freigeschaltetes Modell → 403, freigeschaltetes → 200 |
| 8 | `Quota_MessageLimitPerDay` | Nachrichtenlimit/Tag: 3. Anfrage → 429 |
| 9 | `Quota_TokenLimitPerRequest` | Tokenlimit pro Anfrage → 429 |
| 10 | `Quota_ParallelRequests_NoBypass` | 6 parallele Anfragen bei Limit 3 → genau 3× 200, 3× 429, keine 5xx |
| 11 | `Keys_StoredHashed_PlaintextOnlyOnce` | DB enthält nur SHA-256-Hash; Listen-Endpunkt ohne Klartext |
| 12 | `Build_WebAndDesktop_Succeed` | `dotnet build` für Nexora.Web und Nexora.Desktop fehlerfrei |
| 13 | `App_DeviceLoginFlow_Works` | Nativer App-Login mit Gerätekennung + Refresh-Flow |
| 14 | `LegacyApi_V1Apps_StillWorks` | `GET api/v1/apps` → 200; unbekannte ID → 404 |
| 15 | `WharfPublicSchema_Unchanged` | Keine Nexora-Tabelle in `public`; `nexora` enthält Tabellen |
| 16 | `Bootstrap_OnlyOnce_ThenLocked` | Zweiter Bootstrap → 409; `needsSetup=false` |
| + | `Health_ReturnsUp` | `/health` → up |
| + | `SetupStatus_NeedsSetupIsFalse` | Bootstrap-Status nach Einrichtung |
| + | `AdminLogin_AfterBootstrap_ReturnsTokens` | Admin-Login liefert Token inkl. Rolle Admin |
| + | `TestDatabase_ContainsAllLicensingTables` | Test-DB-Schema vollständig |

### Testumgebung (warum Wharf unberührt bleibt)
- Die Integrations-Tests laufen gegen eine **isolierte, dateibasierte SQLite-Testdatenbank** (`NexoraAppFactory`), niemals gegen die Wharf-DB.
- Der echte LiteLLM-Dienst ist durch einen deterministischen `FakeLiteLlmClient` ersetzt.
- Ergebnis der Schema-Prüfung: EF Core **ignoriert das `nexora`-Schema bei SQLite** und legt neutrale Tabellennamen an. Es wurden 24 Tabellen + `sqlite_sequence` erstellt (`AspNet*`, `Products`, `Licenses`, `LicenseKeys`, `AiModels`, `UsagePeriods`, `ChatMessages`, …). Das `nexora`-Schema ist damit SQLite-kompatibel (keine Migration/Produktionsänderung nötig).

---

## 5. Docker / Container-Bereitstellung (Zusatzauftrag)

**Neue/geänderte Dateien:**

| Datei | Zweck |
|---|---|
| `docker-compose.yml` | Basis-Stack: `nexora-api` + `nexora-web`, **externe** PostgreSQL; alle Einstellungen über `.env`; Healthchecks; nicht privilegiert |
| `docker-compose.local-db.yml` | Optional: lokale Test-PostgreSQL (nur für Tests; `down -v` warnt vor Datenverlust) |
| `docker-compose.litellm.yml` | Optional: lokale LiteLLM-Instanz (nur falls keine externe existiert) |
| `src/Nexora.Api/Dockerfile` | Mehrstufig (SDK → aspnet), `.NET 10`, `USER app` (nicht-Root), curl nur für Healthcheck |
| `src/Nexora.Web/Dockerfile` | Mehrstufig, `.NET 10`, nicht-Root |
| `.dockerignore` | Schließt `.env`, `bin/obj`, Tests etc. aus dem Build-Kontext |
| `.env.example` | Erweitert um Compose-Variablen (Ports, DB-Bestandteile, Log-Level) |
| `DEPLOYMENT.md` | Vollständige Install-/Betriebsanleitung (Backup, Migration, HTTPS/Reverse-Proxy, Updates, Rollback) |
| `deploy/litellm/config.yaml.example` | Muster für lokale LiteLLM-Konfiguration |

**Wichtige Architektur-Entscheidungen:**
- **Kein zweiter PostgreSQL-Server**: Der Stack verbindet sich mit der vorhandenen Instanz (Schema `nexora` per Code fixiert; `nexora.__EFMigrationsHistory`). Das Schema `public` (Wharf) bleibt unberührt (nicht in Download-Zugriffen enthalten, keine Compose-Änderung).
- **Kein fester `localhost`**: Web spricht die API intern als `http://nexora-api:8080` (`ApiBaseUrl`), Browser erreicht nur das Panel. Ports innen fix 8080, außen via `.env` (Standard `5199`/`5100`).
- **Kontrollierte Migrationen**: neuer Modus `nexora-api --migrate` (wendet nur ausstehende Migrationen an und beendet sich), ausgeführt als `docker compose run --rm nexora-api --migrate`. `NEXORA_AUTO_MIGRATE=0` Standard; keine destruktiven Operationen.
- **Reverse-Proxy-Support**: `NEXORA_USE_FORWARDED_HEADERS=1` setzt `UseForwardedHeaders` (X-Forwarded-For/Proto/Host) in API **und** Web; CORS-Standard in Produktion jetzt sicher (leer ⇒ keine Cross-Origin-Erlaubnis statt `AllowAnyOrigin`).
- **Bootstrap hinter Proxy**: Das Web-Panel reicht `X-Bootstrap-Secret` serverseitig an die API weiter (Secret nie im Browser).
- **Healthchecks**: API `/health/ready` (prüft zusätzlich DB-Erreichbarkeit via `CanConnectAsync`), Web `/health`; `service_healthy`-Kopplung.

**In dieser Umgebung tatsächlich verifiziert (kein Docker installiert):**
- `dotnet build NexoraHub.slnx` → **0 Fehler** (ASPDEPR005-Warnung durch Umstieg auf `KnownIPNetworks` beseitigt).
- `dotnet test` → **20/20 grün** nach den Änderungen.
- Exakter Publish-Befehl der Dockerfiles (`-c Release --no-restore -o … -p:UseAppHost=false`) für API und Web → erfolgreich.
- API im **Produktionsmodus** gestartet: `/health` → 200 `up`, `/health/ready` → 200 `ready` gegen echte PostgreSQL.
- `Nexora.Api.dll --migrate` gegen die echte DB → „Keine ausstehenden Migrationen" (History aus `nexora."__EFMigrationsHistory"`).
- Web-Panel im Produktionsmodus gestartet (ApiBaseUrl → API): `/health` → 200, Startseite rendert (enthält „Nexora"/Ersteinrichtung).
- YAML-Syntax aller 3 Compose-Dateien + LiteLLM-Example valide (PyYAML); alle 25 `${…}`-Referenzen in `docker-compose.yml` in `.env`/`.env.example` abgedeckt.
- Logcheck: keine DB-Passwörter, JWT- oder LiteLLM-Schlüssel in Logs.

**Nicht möglich (offen dokumentiert, kein Docker):** `docker compose config`, `docker build` und Container-Start inkl. Healthchecks. Diese abschließende Prüfung erfolgt auf dem Zielserver gemäß `DEPLOYMENT.md` (§7–§9).

---

## 6. Build- und Startbefehle

**Gesamt-Build (Lösung):**
```
dotnet build NexoraHub.slnx
```
Ergebnis: **0 Fehler** (Warnungen: NU1903 „Microsoft.OpenApi 2.3.0" und „SQLitePCLRaw.lib.e_sqlite3 2.1.11" – bekannte Schwachstellen-Hinweise; NU1603 FluentAssertions 8.0.2 → 8.1.0 aufgelöst).

**Tests:**
```
dotnet test tests\Nexora.Api.Tests\Nexora.Api.Tests.csproj
```

**API starten** (liest `.env`):
```
NEXORA_AUTO_MIGRATE=0 dotnet run --project src\Nexora.Api --urls http://localhost:5199
```

**Web-Admin starten** (erwartet die API unter `ApiBaseUrl`, Standard `http://localhost:5199`):
```
dotnet run --project src\Nexora.Web
```

**Desktop-App starten** (Debug):
```
dotnet run --project src\Nexora.Desktop
```

---

## 7. Ausgelieferte EXE (verifiziert)

Self-contained Publish für Windows x64:

```
dotnet publish src\Nexora.Desktop\Nexora.Desktop.csproj -c Release -r win-x64 --self-contained true -o publish\NexoraAI-win-x64
```

**Pfad:** `C:\Users\david\Desktop\NexoraHub\publish\NexoraAI-win-x64\NexoraAI.exe`

Verifikation: Die EXE wurde gestartet und blieb stabil aktiv (Prozess nach mehreren Sekunden noch laufend) – kein XAML-/Ressourcen-Crash.

---

## 8. Offene Punkte / Hinweise

1. **LiteLLM nicht konfiguriert**: In `.env` sind `NEXORA_LITELLM_URL`/`NEXORA_LITELLM_KEY` leer, es wurde keine LiteLLM-Instanz gefunden. Bis zur Konfiguration liefert `POST /api/v1/chat/send` bewusst **503** („KI-Dienst nicht konfiguriert"); alle Lizenz-/Limit-/Auth-Flows sind davon unabhängig und getestet.
2. **Sicherheit der lokalen `.env`**: Enthält echten DB-Passwert und JWT-Schlüssel. Datei ist in `.gitignore` (`NEXORA_DB`, `NEXORA_JWT_KEY` müssen in Produktion gesetzt werden).
3. **Debug-Reste im Repo**: `check_remote.cs`, `temp_check/` (nicht Teil der Lösung) können entfernt werden.
4. **`tests/Nexora.Web.Tests`** enthält nur einen Platzhaltertest; Web wird über den Build-Test in `InfrastructureTests` und den Lösungs-Build abgedeckt.
5. **Package-Hinweise** (NU1903): `Microsoft.OpenApi 2.3.0` (Swashbuckle, nur Development) und `SQLitePCLRaw.lib.e_sqlite3 2.1.11` (nur Testabhängigkeit) sind als Schwachstellen markiert – bei Bedarf aktualisieren.
6. **Docker endgültig prüfen**: In dieser Umgebung war kein Docker installiert; die finale Verifikation von `docker compose config`, `docker compose build` und `up -d` + Healthcheck läuft auf dem Zielserver nach `DEPLOYMENT.md` (§7–§9).
