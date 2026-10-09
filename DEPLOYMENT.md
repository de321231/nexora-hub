# Nexora Hub – Deployment mit Docker Compose

Diese Anleitung beschreibt, wie Sie den **Nexora Lizenzserver (API)** und das
**Admin-Panel (Web)** auf einem Server (z. B. Proxmox-LXC mit Docker) mit
Docker Compose installieren, betreiben und aktualisieren.

> **Wichtig vorab:** Nexora verwendet ausschließlich das PostgreSQL-Schema
> **`nexora`**. Das vorhandene Schema **`public`** gehört Ihrer Wharf-Instanz
> und wird von Nexora **niemals** verändert, gelöscht oder genutzt.
> Es wird bewusst **kein zweiter Datenbankserver** eingerichtet – Nexora
> verbindet sich mit der vorhandenen PostgreSQL-Instanz.

Die native Windows-App (`src/Nexora.Desktop`) wird unverändert als Exe gebaut
und verteilt – sie benötigt **keinen** Docker-Container.

---

## 1. Architektur & Ports

```
                        Internet / LAN
                              │
                    ┌─────────▼─────────┐
                    │  Reverse Proxy    │   (optional, empfohlen für HTTPS)
                    │  Caddy / nginx    │
                    └──┬────────────┬───┘
                       │            │
         ┌─────────────▼──┐   ┌─────▼──────────┐
         │  nexora-web    │   │  nexora-api    │   ┌──────────────────┐
         │  Blazor Server │   │  Lizenzserver  │──▶│ externe          │
         │  (Admin-Panel) │   │  (ASP.NET API) │   │ PostgreSQL       │
         └────────┬───────┘   └────┬───┬───────┘   │ (Schema nexora)  │
                  │                │   │           └──────────────────┘
                  └── internes Docker-Netzwerk "nexora" ──┘
                                    │   │
                                    │   └── NEXORA_LITELLM_URL/KEY (serverseitig)
                                    └── extern: Windows-App / API-Clients
```

| Komponente | Port im Container | Port nach außen (Standard) | Zweck |
|---|---|---|---|
| `nexora-api` | `8080` (intern fix) | `5199` | Lizenz-/User-/Chat-API |
| `nexora-web` | `8080` (intern fix) | `5100` | Admin-Panel, Browser |
| PostgreSQL | extern / optional intern | **nicht** veröffentlicht | Datenbank (Schema `nexora`) |
| LiteLLM | extern / optional intern `4000` | **nicht** veröffentlicht | KI-Backend |

Intern (im Docker-Netzwerk) spricht das Web-Panel die API unter
`http://nexora-api:8080` an (`ApiBaseUrl`). Diese Adresse ist **keine**
Browser-Adresse – der Browser erreicht nur das Panel. So gibt es keine
fest codierten `localhost`-URLs, die im Container oder von anderen Geräten
nicht funktionieren.

---

## 2. Voraussetzungen

Auf dem Server:

- **Docker Engine** ≥ 24 und **Docker Compose Plugin** (`docker compose version`)
- optional `git` (falls Sie direkt aus dem Repository deployen)
- Zugriff auf die vorhandene PostgreSQL-Instanz (Host, Port, User, Passwort)
- optional: Zugangsdaten zu einer **externen LiteLLM-Instanz** (URL, API-Key)

Prüfen:

```bash
docker --version
docker compose version
```

> Hinweis: Die fertige `.exe` bzw. die `.NET 10`-SDK sind auf dem Server **nicht**
> nötig – alles läuft in Containern. Nur um die Images **zu bauen** benötigt der
> Build-Schritt die SDK-Images (werden automatisch von Docker gezogen).

---

## 3. Repository auf den Server bringen

```bash
# Variante A: per git
git clone <ihr-repository> nexora
cd nexora

# Variante B: per Übertragung (z. B. scp / rsync / Proxmox-Container aus Host-Dir)
# Achten Sie darauf, dass .env und alle Docker-Dateien übertragen werden:
cd nexora
```

---

## 4. `.env` anlegen und ausfüllen

Die Compose-Datei liest alle Einstellungen aus `.env`. Diese Datei ist in
`.gitignore` und wird **nicht** eingecheckt.

```bash
cp .env.example .env
chmod 600 .env          # nur der Eigentümer darf sie lesen
nano .env
```

### 4.1 Mindestpflicht für den Produktivbetrieb

Compose **bricht bewusst ab**, wenn diese Werte fehlen (keine stillen
Standardpasswörter!):

- `NEXORA_JWT_KEY` – JWT-Schlüssel, **mindestens 32 Byte**. Erzeugen:
  ```bash
  openssl rand -base64 48
  ```
- `NEXORA_DB_HOST`, `NEXORA_DB_PORT`, `NEXORA_DB_NAME`, `NEXORA_DB_USER`,
  `NEXORA_DB_PASSWORD` – Zugangsdaten Ihrer **vorhandenen** PostgreSQL-Instanz.

> Für die Wharf-Instanz ergeben sich die Werte direkt aus der bestehenden
> `NEXORA_DB=`-URL, z. B. aus
> `postgres://BENUTZER:PASSWORT@192.168.188.198:32770/app`:
>
> | Variablen | Wert |
> |---|---|
> | `NEXORA_DB_HOST` | `192.168.188.198` |
> | `NEXORA_DB_PORT` | `32770` |
> | `NEXORA_DB_NAME` | `app` |
> | `NEXORA_DB_USER` | `BENUTZER` |
> | `NEXORA_DB_PASSWORD` | `PASSWORT` |
>
> Empfehlung: Um Wharf und Nexora sauber zu trennen, legen Sie alternativ eine
> **eigene Datenbank** (z. B. `nexorahub`) auf demselben Server an und geben
> Nexora einen eigenen Benutzer mit Rechten auf **nur diese Datenbank/Schema**.

### 4.2 Empfohlene Werte

```bash
# Umgebung
NEXORA_ENVIRONMENT=Production
NEXORA_LOG_LEVEL=Information

# Ports (Standard reicht meist)
NEXORA_API_HTTP_PORT=5199
NEXORA_WEB_HTTP_PORT=5100

# Migrationen: Standard kontrolliert (aus)
NEXORA_AUTO_MIGRATE=0

# Nur wenn ein Reverse Proxy vorgeschaltet ist:
NEXORA_USE_FORWARDED_HEADERS=1

# Optionale Absicherung des einmaligen Admin-Bootstraps
NEXORA_BOOTSTRAP_SECRET=$(openssl rand -base64 32)
```

Optional (nur bei vorhandener externer LiteLLM-Instanz):

```bash
NEXORA_LITELLM_URL=https://ihr-litellm.example.com
NEXORA_LITELLM_KEY=<api-key>
NEXORA_LITELLM_DEFAULT_MODEL=openai/gpt-4o-mini
```

> **Geheimnisse** (JWT-Key, DB-Passwort, LiteLLM-Key, Bootstrap-Secret) stehen
> **nur** in `.env`, gehören nie in Git, nie in Images und nie in Logs.
> Ein sicherer Ort wären alternativ Docker-Secrets oder Ihr Passwort-Manager.

---

## 5. Verbindung prüfen (PostgreSQL & LiteLLM)

Vertippte Zugangsdaten sind die häufigste Fehlerquelle. Prüfen Sie daher zuerst
**ohne Docker**:

```bash
# a) PostgreSQL ist erreichbar?
#    (auf dem Docker-Host, ggf. in die LXC-Shell wechseln)
psql "postgres://BENUTZER:PASSWORT@192.168.188.198:32770/app" \
  -c "SELECT current_database(), current_user;"

# Schema-Existenzen prüfen (Achtung: public gehört Wharf, nur lesen!)
psql "postgres://BENUTZER:PASSWORT@192.168.188.198:32770/app" -c \
  "SELECT schema_name FROM information_schema.schemata ORDER BY schema_name;"

# Keine Nexora-Tabellen dürfen in "public" liegen:
psql "postgres://BENUTZER:PASSWORT@192.168.188.198:32770/app" -c \
  "SELECT table_schema||'.'||table_name FROM information_schema.tables
   WHERE table_schema='public'
     AND (table_name ILIKE '%nexora%' OR lower(table_name) LIKE 'aspnet%'
          OR table_name ILIKE '%license%');"
# → Ergebnis: leer (0 Zeilen)
```

```bash
# b) LiteLLM ist erreichbar? (falls vorhanden)
curl -fsS -H "Authorization: Bearer $NEXORA_LITELLM_KEY" \
  "$NEXORA_LITELLM_URL/v1/models" | head -c 400
```

---

## 6. Datenbank-Sicherung (vor der ersten Migration!)

EF-Core-Migrationen sind additiv und nicht destruktiv, aber ein Backup ist
trotzdem Pflicht **vor jeder** Migration oder jedem Update.

**Methode A – mit lokalem `pg_dump`:**

```bash
pg_dump --host=192.168.188.198 --port=32770 --username=BENUTZER \
        --dbname=app --schema=nexora --format=custom \
        --file=nexora_$(date +%F).dump
```

**Methode B – ohne lokales `pg_dump` (via Docker-Container):**

```bash
docker run --rm -e PGPASSWORD='PASSWORT' postgres:16-alpine \
  pg_dump --host=192.168.188.198 --port=32770 --username=BENUTZER \
          --dbname=app --schema=nexora --format=custom \
  > nexora_$(date +%F).dump
```

> Der Parameter `--schema=nexora` stellt sicher, dass **nur** das Nexora-Schema
> gesichert wird und niemals das `public`-Schema von Wharf.

**Wiederherstellen** (nur im Schadensfall, niemals im laufenden Betrieb):

```bash
docker run --rm -e PGPASSWORD='PASSWORT' postgres:16-alpine \
  pg_restore --host=192.168.188.198 --port=32770 --username=BENUTZER \
             --dbname=app --schema=nexora --clean --if-exists \
  < nexora_$(date +%F).dump
```

Beachten Sie: `--clean` löscht **nur Objekte innerhalb des `nexora`-Schemas**
(vorherige Objekte), `--schema=nexora` beschränkt alles darauf. Prüfen Sie nach
einer Wiederherstellung [`docker compose ps`](#8-build-start-und-gesundheitschecks).

---

## 7. Migrationen kontrolliert ausführen

Es gibt zwei Wege; der **kontrollierte** ist Standard und ausdrücklich empfohlen:

```bash
# 1. Images bauen
docker compose build

# 2. Migrationen kontrolliert anwenden (einmaliger Lauf, beendet sich danach)
docker compose run --rm nexora-api --migrate
```

Die Ausgabe sollte so aussehen:

```
[INF] Keine ausstehenden Migrationen – Datenbank ist aktuell.
# ODER, wenn es ausstehende gab:
[INF] Wende 1 ausstehende Migration(en) an: 2026xxxx_...
[INF] Migrationen erfolgreich angewendet.
```

> Alternativ kann `NEXORA_AUTO_MIGRATE=1` gesetzt werden, dann wendet der
> API-Container die Migrationen beim Start selbst an (weiterhin additiv).
> Standard ist aus – ausdrücklich empfohlen für kontrollierten Betrieb.

`docker compose run --rm` startet einen **einmaligen** Container desselben
Bildes, wendet die Migrationen an und beendet sich. Es wird keine Text-Datei
verändert (siehe hierzu keine destruktive DB-Operation: `_EFMigrationsHistory`
wird in Schema `nexora` geführt, `public` bleibt unberührt).

---

## 8. Build, Start und Gesundheitschecks

```bash
# Images bauen
docker compose build

# Container starten (im Hintergrund)
docker compose up -d

# Status anzeigen (inkl. HEALTHY der Healthchecks)
docker compose ps

# Logs der API verfolgen
docker compose logs -f nexora-api

# Logs des Panels verfolgen
docker compose logs -f nexora-web
```

Der API-Healthcheck prüft `/health/ready` (erreichbar **und** Datenbank erreichbar).
Das Panel startet erst, wenn die API `healthy` ist (`depends_on: service_healthy`).

```bash
# Healthchecks manuell prüfen
curl -fsS http://localhost:5199/health/ready    # → {"status":"ready",...}
curl -fsS http://localhost:5100/health          # → {"status":"up",...}
```

Wenn ein Container nicht `healthy` wird, Logs prüfen:

```bash
docker compose logs nexora-api
docker inspect --format '{{json .State.Health}}' nexora-nexora-api-1
```

---

## 9. Admin-Panel öffnen und ersten Administrator einrichten

Öffnen Sie im Browser:

- ohne Reverse Proxy: `http://<server-ip>:5100`
- mit Reverse Proxy: `https://panel.example.com`

Der Bootstrap-Endpunkt ist **einmalig** und danach dauerhaft gesperrt; es gibt
**keine** Standard-Admin-Zugangsdaten und die Registrierung vergibt niemals
Adminrechte.

**Ihre erste Anmeldung als Administrator – drei Wege:**

1. **Direkt auf dem Server** (Empfehlung bei erster Inbetriebnahme): Die
   Ersteinrichtung ist per Loopback immer erlaubt:
   ```bash
   curl -sS -X POST http://localhost:5199/api/v1/admin/setup \
     -H "Content-Type: application/json" \
     -d '{"email":"admin@example.com","password":"MIN8Zeichen1!","fullName":"Admin"}'
   ```
   oder öffnen Sie das Panel einmal **auf dem Server selbst**
   (`http://localhost:5100/setup`) und füllen das Formular aus.

2. **Per Web-Panel hinter einem Reverse Proxy:** Dazu vor dem ersten Start in
   `.env` ein `NEXORA_BOOTSTRAP_SECRET` setzen (siehe §4.2). Das Web-Panel
   reicht das Geheimnis **serverseitig** als `X-Bootstrap-Secret` an die API
   weiter – der Browser erhält es nie. Danach `/setup` öffnen und ausfüllen.

3. **Per API mit Secret** (z. B. aus einer entfernten Shell):
   ```bash
   curl -sS -X POST https://api.example.com/api/v1/admin/setup \
     -H "Content-Type: application/json" \
     -H "X-Bootstrap-Secret: <secret aus .env>" \
     -d '{"email":"admin@example.com","password":"MIN8Zeichen1!","fullName":"Admin"}'
   ```

> Ohne Secret gilt: Der Setup-Endpunkt akzeptiert in Produktion nur Anfragen
> von `127.0.0.1` (Loopback). Das verhindert eine Ersteinrichtung von außen.

---

## 10. HTTPS und Reverse Proxy (empfohlen für Produktion)

Nexora liefert **keinen** eigenen Reverse Proxy mit – verwenden Sie Ihren
vorhandenen (Caddy, nginx, Traefik, Proxmox-Hostproxy …). Für HTTPS ist ein
Proxy der Standardweg; setzen Sie zusätzlich in der `.env`:

```bash
NEXORA_USE_FORWARDED_HEADERS=1
NEXORA_API_BIND_IP=127.0.0.1    # API nicht direkt öffentlich binden
NEXORA_WEB_BIND_IP=127.0.0.1    # Panel nicht direkt öffentlich binden
```

Dann binden die Container auf `127.0.0.1` und nur der Proxy hat Zugriff.
Nach Änderungen an `.env` einen Neustart durchführen:

```bash
docker compose up -d
```

### Caddy (Beispiel, automatische Let’s-Encrypt-Zertifikate)

```caddyfile
panel.example.com {
    reverse_proxy 127.0.0.1:5100
}

api.example.com {
    reverse_proxy 127.0.0.1:5199
}
```

### nginx (Beispiel)

```nginx
server {
    listen 443 ssl;
    server_name panel.example.com;
    ssl_certificate     /etc/ssl/certs/fullchain.pem;
    ssl_certificate_key /etc/ssl/private/key.pem;

    location / {
        proxy_pass http://127.0.0.1:5100;
        proxy_http_version 1.1;
        proxy_set_header Host              $host;
        proxy_set_header X-Forwarded-For   $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_set_header Upgrade           $http_upgrade;   # Blazor Server / SignalR
        proxy_set_header Connection        "upgrade";
        proxy_buffering off;
    }
}
```

> **Blazor Server** (das Admin-Panel) braucht **WebSockets**. Im nginx-Beispiel
> sind die `Upgrade`-Header deshalb gesetzt. Caddy und Traefik handhaben das
> automatisch. Ohne funktionierende WebSockets kann sich das Panel nicht verbinden.

Durch `NEXORA_USE_FORWARDED_HEADERS=1` vertraut die App den
`X-Forwarded-For/Proto/Host`-Headern. **Sicherheitsvoraussetzung:** Die
Container dürfen dann ausschließlich über den Proxy erreichbar sein (Bind auf
`127.0.0.1`), sonst kann ein Angreifer Header fälschen.

---

## 11. Updates, Rollback, Backup & Wiederherstellung

### Updates durchführen (Standard-Ablauf)

```bash
git pull                                   # neuen Quellcode holen
cp .env.example .env                       # nur wenn Zielserver neu; sonst bestehendes .env behalten
docker compose build                       # neue Images bauen
docker compose run --rm nexora-api --migrate   # Datenbankschema kontrolliert aktualisieren
docker compose up -d                       # Container auf neues Image umstellen
docker compose ps                          # Healthchecks prüfen
docker compose logs -f nexora-api          # Logs beobachten
```

> **`docker compose down -v` ist kein normaler Stopp- oder Update-Befehl.**
> `-v` löscht benannte Volumes (z. B. `nexora_pgdata` der lokalen Test-DB) und
> führt zu Datenverlust. Zum Stoppen reicht `docker compose down`.

### Stoppen / Starten

```bash
docker compose down        # Container stoppen und entfernen; Daten bleiben erhalten
docker compose start       # Container wieder starten (ohne Neubauen)
docker compose restart nexora-api
```

### Rollback

Die Images sind mit `NEXORA_IMAGE_TAG` (Standard `latest`) getaggt. Strategien:

1. **Tag-Pinning:** In `.env` `NEXORA_IMAGE_TAG=v2026-10-01` setzen, dann
   `docker compose build && docker compose up -d`. Führen Sie bei jedem
   Release einen eindeutigen Tag ein.
2. **Quellcode-Rollback:** alten Stand aus Git auschecken (z. B. einen Tag),
   dann `docker compose build && docker compose up -d`.

**Achtung:** EF-Migrationen sind **vorwärtsgerichtet**. Ein Rollback auf eine
**ältere** Datenbankstruktur erfordert eine Wiederherstellung des Backups aus
§6 – der Code-Fallback allein reicht dann nicht.

### Backup & Wiederherstellung

Siehe §6. Führen Sie regelmäßig (z. B. per Cron) ein Backup **nur des
`nexora`-Schemas** durch – das `public`-Schema von Wharf wird nie angefasst:

```bash
docker run --rm -e PGPASSWORD='PASSWORT' postgres:16-alpine \
  pg_dump --host=192.168.188.198 --port=32770 --username=BENUTZER \
          --dbname=app --schema=nexora --format=custom \
  > /backup/nexora_$(date +%F).dump
```

---

## 12. Sicherheit und Betriebshinweise

- **Keine Standard-Zugangsdaten:** Der erste Admin wird über den einmaligen
  Bootstrap erstellt; die Registrierung vergibt nie Adminrechte. Ohne
  `NEXORA_BOOTSTRAP_SECRET` geht der Bootstrap in Produktion nur von `127.0.0.1`.
- **Keine Geheimnisse in Images/Logs:** `.env` ist per `.dockerignore` vom
  Build-Kontext ausgeschlossen und liegt nie im Image. Die API gibt weder
  `NEXORA_LITELLM_KEY` noch Lizenzschlüssel an Clients oder in Logs aus.
- **Nicht privilegiert:** Beide Container laufen als nicht privilegierter
  Benutzer `app` (UID 1654) mit `no-new-privileges`; keine `privileged`-Flags.
- **Ports:** Nur API (`5199`) und Web (`5100`) werden veröffentlicht.
  PostgreSQL und LiteLLM in optionalen Overrides sind nur intern erreichbar.
- **Migrationen:** additiv und kontrolliert (§7); nie `down -v`, nie
  `EnsureDeleted`; die Migrationshistorie liegt im Schema `nexora`.
- **Reverse Proxy:** HTTPS vor Produktion, `X-Forwarded-*` nur bei
  ausschließlicher Proxy-Erreichbarkeit aktivieren.
- **Logs prüfen:** Die Logs enthalten keine Passwörter, Licenseschlüssel oder
  JWTs. Bei Verdacht: `docker compose logs nexora-api | grep -i -E "password|token|secret"`.

---

## 13. Unterstützte Varianten (LiteLLM)

**Variante A – externe, vorhandene LiteLLM-Instanz (Standard):**

Setzen Sie ausschließlich `NEXORA_LITELLM_URL` und `NEXORA_LITELLM_KEY` in
`.env`. Die API ruft die tatsächlich verfügbaren Modelle dynamisch über
`GET /v1/models` ab; das Panel zeigt nur diese an. Der Schlüssel wird nie an
den Browser oder die Windows-App übertragen. Ist LiteLLM nicht erreichbar,
liefert die API eine verständliche Fehlermeldung (503) – es werden keine
erfundenen Modelle zurückgegeben. Ob Models der externen Instanz funktionieren,
kontrollieren Sie mit dem Befehl aus §5b.

**Variante B – lokale LiteLLM-Instanz (optional, nur ohne externe Instanz):**

```bash
cp deploy/litellm/config.yaml.example deploy/litellm/config.yaml
nano deploy/litellm/config.yaml          # echte Provider-Zugangsdaten eintragen
# in .env setzen:
#   NEXORA_LITELLM_URL=http://litellm:4000
#   NEXORA_LITELLM_KEY=<master_key>
docker compose -f docker-compose.yml -f docker-compose.litellm.yml up -d
```

---

## 14. Vollständige Dateiübersicht

| Datei | Zweck |
|---|---|
| `docker-compose.yml` | Basis-Stack: API + Web, externe PostgreSQL, alle Env-Variablen |
| `docker-compose.local-db.yml` | **Optional:** lokale Test-PostgreSQL (nicht für Produktion) |
| `docker-compose.litellm.yml` | **Optional:** lokale LiteLLM-Instanz (nur ohne externe) |
| `src/Nexora.Api/Dockerfile` | Mehrstufiges API-Image (.NET 10) |
| `src/Nexora.Web/Dockerfile` | Mehrstufiges Web-Image (.NET 10) |
| `.dockerignore` | Schließt `.env`, `bin/obj`, Tests usw. aus dem Build-Kontext aus |
| `.env.example` | Musterkonfiguration ohne Geheimnisse |
| `deploy/litellm/config.yaml.example` | Muster für lokale LiteLLM-Konfiguration |
| `DEPLOYMENT.md` | Diese Anleitung |

---

## 15. In der aktuellen Entwicklungsumgebung geprüftes

In der Umgebung, in der diese Dateien erstellt wurden, stand **kein Docker**
zur Verfügung. Deshalb wurden die Dockerfiles/Compose-Dateien **nicht mit
`docker compose config` und nicht per `docker build` ausgeführt** –
es wird hier **nichts erfolgreich behauptet**, was nicht tatsächlich lief.

Tatsächlich durchgeführt:

- Vollständiger **`dotnet build`** der Lösung (0 Fehler) nach den
  produktionsrelevanten Erweiterungen.
- **`dotnet publish -r linux-x64`** für API und Web – der exakte
  Publish-Befehl aus den Dockerfiles wurde als nativer Linux-Build verifiziert.
- **Testsuite: 20/20 grün** nach den Erweiterungen (in-memory/SQLite-Struktur
  entspricht der `nexora`-Schema-Struktur).
- Lokaler API-Start mit `/health`, `/health/ready` (DB-Erreichbarkeit) und
  dem kontrollierten Migrationsmodus `--migrate` gegen die echte
  PostgreSQL-Instanz verifiziert.
- YAML-Syntax der Compose-/Override-Dateien mit einem YAML-Parser validiert.

Auf dem Zielserver (mit Docker) sind daher **§7–§9 sowie §11** als letzter
Verifikationsschritt auszuführen. Die Images setzen stable Tags
`mcr.microsoft.com/dotnet/sdk:10.0` und
`mcr.microsoft.com/dotnet/aspnet:10.0` voraus.

---

### Kurzstart (Zusammenfassung)

```bash
cp .env.example .env && chmod 600 .env   # Werte eintragen (§4)
openssl rand -base64 48                  # → NEXORA_JWT_KEY

docker compose build
docker compose run --rm nexora-api --migrate
docker compose up -d
docker compose ps                       # beide Container sollen healthy sein
curl -fsS http://localhost:5199/health/ready
# Browser: http://<server-ip>:5100 → Ersteinrichtung (§9)
```