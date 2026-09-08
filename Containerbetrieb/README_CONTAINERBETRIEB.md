# WebUI – Containerbetrieb

## 1. Zweck und Geltungsbereich

`Containerbetrieb/` enthält die allgemeine Container-Vorlage für den WebUI-Betrieb auf ARM64 und AMD64.

Die Vorlage ist von der konkreten Laufzeitinstallation getrennt. Eine produktiv oder zu Testzwecken verwendete `.env`, Secret-Dateien, persistente Laufzeitdaten und Releaseartefakte gehören nicht in den Quellbaum.

Die WebUI arbeitet gegenüber Paperless-ngx fachlich read-only. Paperless-ngx bleibt alleinige Quelle für Dokumente, Metadaten, Benutzer und Rechte.

## 2. Voraussetzungen

Erforderlich sind:

- Docker mit Compose-Unterstützung;
- ein für die Zielarchitektur gebautes und lokal vorhandenes WebUI-Image;
- ein erreichbares Paperless-ngx;
- ein erreichbarer OIDC-Provider;
- ein Reverse Proxy für den normalen HTTPS-Zugriff;
- die in Abschnitt 6 beschriebenen Secret-/Schutzdateien.

Der Container lauscht intern fest auf Port `8080`.

## 3. Quellvorlagen

```text
Containerbetrieb/
├── .env.example
├── compose.yaml.example
├── Dockerfile
├── validate-runtime-config.sh
├── start-container.sh
└── README_CONTAINERBETRIEB.md
```

Das `Dockerfile` verwendet den Projektwurzelordner als Build-Kontext und ist nicht auf ARM64 oder AMD64 fest verdrahtet.

### Container-Image bereitstellen

Für den Containerbetrieb muss vor dem Start ein zur Zielarchitektur passendes WebUI-Image lokal vorhanden sein.

Wird für die verwendete OrdnerBrowse-Version ein vorgebautes Container-Image als TAR-Datei bereitgestellt, muss die zur Zielarchitektur passende TAR-Datei zusammen mit der zugehörigen SHA-256-Prüfsumme verwendet werden.

Vor dem Import ist die SHA-256-Prüfsumme der TAR-Datei zu überprüfen. Anschließend kann das Image mit der jeweiligen Container-Laufzeit lokal importiert werden.

Alternativ kann das Image aus dem zugehörigen veröffentlichten Quellstand selbst gebaut werden.

Die Compose-Vorlage verwendet `pull_policy: never`. Ein Container-Image wird deshalb nicht automatisch aus einer Registry geladen.

Falls für eine zukünftige Veröffentlichung zusätzlich eine öffentliche Registry bereitgestellt wird, wird deren Verwendung separat für die betreffende Veröffentlichung dokumentiert.

## 4. Laufzeitstruktur

Die Laufzeitinstallation liegt außerhalb des Quellbaums. Der konkrete Runtime-Root wird installationsabhängig gewählt und liegt außerhalb des Quellbaums.

```text
<Runtime-Root>/
├── compose.yaml
├── .env
├── config/
│   ├── display-prefixes.json
│   └── <Version>/
│       ├── compose.yaml
│       └── .env
├── secrets/
│   ├── oidc_client_secret
│   ├── data_protection_certificate
│   ├── data_protection_certificate_password
│   └── proxy_guard_secret
├── navigation-cache/
├── diagnostic/
│   ├── oidc/
│   └── performance/
├── user-tokens/
├── data-protection-keys/
└── releases/
    └── <Version>/
```

`compose.yaml` wird unverändert aus `compose.yaml.example` übernommen und ist für Haupt- und Rückfallprojekt byteidentisch. Die Standortabhängigkeit der gemeinsamen Runtime-Verzeichnisse wird ausschließlich über `WEBUI_RUNTIME_ROOT` in der jeweiligen `.env` gesteuert.

Für das Hauptprojekt im Runtime-Root gilt:

```text
WEBUI_RUNTIME_ROOT=.
```

Für ein versionsbezogenes Rückfallprojekt unter `config/<Version>/` gilt:

```text
WEBUI_RUNTIME_ROOT=../..
```

`.env` wird aus `.env.example` abgeleitet und enthält nur nicht geheime Werte.

## 5. `.env`-Konfiguration

### `WEBUI_RUNTIME_ROOT`

Pflichtwert für die Host-seitige Auflösung der gemeinsamen Runtime-Verzeichnisse. Für den dokumentierten Containerbetrieb sind ausschließlich folgende Werte vorgesehen:

```text
.
```

für das Hauptprojekt direkt unter `<Runtime-Root>` und:

```text
../..
```

für das versionsbezogene Rückfallprojekt unter `<Runtime-Root>/config/<Version>`.

Die `compose.yaml` bleibt dadurch in beiden Projektpfaden byteidentisch. Die Variable wird ausschließlich für Host-Bind-Pfade wie `secrets/`, `navigation-cache/`, `diagnostic/`, `user-tokens/` und `data-protection-keys/` verwendet.

### `WEBUI_IMAGE`

Pflichtwert. Vollständiger Name und Tag des bereits lokal vorhandenen Images, das gestartet werden soll.

Beispielsyntax:

```text
webui:vX.Y.Z
```

Zulässig ist ein normaler Docker-Image-Name mit optionalem Registry-/Repository-Präfix und Tag. Für den dokumentierten Betriebsweg ist ein eindeutiger Tag zu verwenden. Eine allgemeine `latest`-Strategie ist derzeit nicht festgelegt.

Compose verwendet `pull_policy: never`. Ein fehlendes lokales Image wird deshalb nicht automatisch aus einer Registry geladen.

Sichere Prüfung:

```sh
docker image inspect "$WEBUI_IMAGE" >/dev/null
```

Dabei wird nur geprüft, ob das konfigurierte Image lokal vorhanden ist.

### `APP_UID` und `APP_GID`

Numerische Benutzer- und Gruppen-ID des nicht privilegierten Containerbenutzers.

Beide Werte sind Pflicht. Zulässig sind ausschließlich positive dezimale Ganzzahlen ohne Vorzeichen oder Leerzeichen.

Vorlage:

```text
APP_UID=1654
APP_GID=1654
```

Die Werte müssen zur tatsächlichen Rechtebelegung der Zielumgebung passen. Auf Linux müssen alle schreibbaren Hostverzeichnisse für genau `APP_UID:APP_GID` zugänglich sein. Pauschale Rechte wie `777` und pauschales `chown -R` über den gesamten Runtime-Root sind nicht vorgesehen.

Vor einem Containerstart müssen beide Werte mit der mitgelieferten read-only Laufzeitprüfung validiert werden:

```sh
set -a
. ./.env
set +a
./validate-runtime-config.sh
```

Die Prüfung akzeptiert ausschließlich die kanonische Form `^[1-9][0-9]*$`. Damit werden leere Werte, `0`, negative Werte, Vorzeichen, Leerzeichen, nicht numerische Zeichen und führende Nullen abgelehnt. Die Prüfung startet keinen Container und liest keine Secret-Dateien.

Eine reine Compose-Substitution wie `${APP_UID:?…}` beziehungsweise `${APP_GID:?…}` prüft weiterhin zusätzlich, dass beide Werte gesetzt sind; sie ersetzt die Wertebereichsprüfung jedoch nicht.

### `WEBUI_BIND_ADDRESS`

Pflichtwert. Hostadresse, an die Docker den WebUI-Port bindet.

Reverse Proxy auf demselben Docker-Host:

```text
127.0.0.1
```

Reverse Proxy auf einem anderen Rechner:

```text
<konkrete Host-/LAN-IP des Docker-Hosts>
```

`0.0.0.0` ist nicht der vorgesehene Standard. Bei einem entfernten Reverse Proxy ist der Host-Port zusätzlich durch Firewall-/Netzregeln auf den notwendigen Zugriff zu beschränken.

### `WEBUI_PORT`

Pflichtwert. Freier TCP-Port des Docker-Hosts, der auf den festen Container-Port `8080` zeigt.

Zulässig sind dezimale Ganzzahlen von `1` bis `65535`. Der Port muss auf dem Host frei sein und darf nicht mit einem anderen Dienst kollidieren.

Beispiel:

```text
57040
```

### `TZ`

Zeitzone des Containers. Vorgabewert der Vorlage:

```text
Europe/Berlin
```

### `OIDC_AUTHORITY`

Pflichtwert. Absolute HTTPS-Adresse der OIDC-Authority ohne Query-String oder Fragment.

Beispielsyntax:

```text
https://id.example
```

Ein abschließender `/` wird von der Anwendung bei der Authority normalisiert.

### `OIDC_METADATA_ADDRESS`

Pflichtwert. Absolute HTTPS-Adresse des OIDC-Metadatenendpunkts ohne Query-String oder Fragment.

Beispielsyntax:

```text
https://id.example/.well-known/openid-configuration
```

### `OIDC_CLIENT_ID`

Pflichtwert. Nicht geheime Client-ID der OIDC-Anwendung.

Der Wert wird exakt so verwendet, wie er beim OIDC-Provider für die WebUI registriert ist. Er darf nicht leer sein und darf keine zusätzlichen Anführungszeichen enthalten, die nicht Teil der tatsächlichen Client-ID sind.

Da die Client-ID nicht geheim ist, darf sie zur Prüfung angezeigt werden.

### `PAPERLESS_BASE_URL`

Pflichtwert. Absolute HTTP- oder HTTPS-Basisadresse von Paperless-ngx ohne Query-String oder Fragment.

Zulässig sind zum Beispiel:

```text
http://host.example
http://host.example:8000
https://host.example
```

Ein abschließender `/` ist zulässig und wird von der Anwendung entfernt.

Die Adresse muss aus dem WebUI-Container erreichbar sein. Eine Docker-Servicebezeichnung ist nicht erforderlich; reguläres Routing zu Hostname oder IP ist zulässig.

### `PROXY_GUARD_ENABLED`

Steuert ausschließlich den Proxy Guard:

```text
false
```

oder:

```text
true
```

Der Proxy Guard ist unabhängig von Architektur und OIDC. Bei `false` wird das Guard-Secret nicht benötigt oder gelesen. Bei `true` ist `secrets/proxy_guard_secret` erforderlich.

Ein zusätzlicher OIDC-Ein-/Aus-Schalter ist im regulären Containerbetrieb nicht vorgesehen; OIDC ist hier fest aktiviert.

## 6. Secret- und Schutzdateien

Secretwerte dürfen nicht in `.env`, Compose, Quellcode, URLs, Protokolle oder Screenshots übernommen werden.

Für alle Dateien unter `secrets/` gilt auf Linux als Zielmodell:

```text
Verzeichnis: 700
Dateien:     600
Besitzer:    APP_UID:APP_GID
```

Auf macOS mit Docker Desktop bleibt der Host-Eigentümer technisch der lokale Benutzer; entscheidend ist dort, dass der Container mit `APP_UID:APP_GID` die Dateien im Bind-Mount tatsächlich lesen kann. Die Rechte werden deshalb später praktisch geprüft.

### `oidc_client_secret`

**Pflicht.** OIDC-Client-Secret des Providers.

- Ablage: `<Runtime-Root>/secrets/oidc_client_secret`
- Zielpfad im Container: `/run/secrets/oidc_client_secret`
- Herkunft: aus der OIDC-Clientregistrierung des Providers
- Format: Textdatei mit dem vom Provider gelieferten Secret; ein abschließender Zeilenumbruch ist zulässig
- Besitzer auf Linux: `APP_UID:APP_GID`
- Rechte auf Linux: `600`

Sichere Prüfung ohne Inhaltsausgabe:

```sh
test -s secrets/oidc_client_secret
```

Die Prüfung bestätigt nur, dass eine nicht leere Datei vorhanden ist; der Secretwert wird nicht ausgegeben.

### `data_protection_certificate`

**Pflicht im aktuellen Production-Modell.** PKCS#12-Datei (`PFX`/`P12`) mit privatem Schlüssel zur Verschlüsselung der ASP.NET-Core-Data-Protection-Schlüssel.

- Ablage: `<Runtime-Root>/secrets/data_protection_certificate`
- Zielpfad im Container: `/run/secrets/data_protection_certificate`
- Herkunft: vorhandenes, für diese WebUI vorgesehenes Data-Protection-Zertifikat
- Besitzer auf Linux: `APP_UID:APP_GID`
- Rechte auf Linux: `600`
- Die Datei enthält privates Schlüsselmaterial und ist vertraulich.

Sichere Prüfung ohne Ausgabe des Zertifikatinhalts:

```sh
test -s secrets/data_protection_certificate
```

Eine inhaltliche PKCS#12-Prüfung darf nur lokal erfolgen, ohne private Schlüssel- oder Kennwortinhalte auszugeben.

### `data_protection_certificate_password`

**Pflicht im aktuellen Production-Modell.** Kennwort der PKCS#12-Datei.

- Ablage: `<Runtime-Root>/secrets/data_protection_certificate_password`
- Zielpfad im Container: `/run/secrets/data_protection_certificate_password`
- Herkunft: Kennwort des verwendeten Data-Protection-Zertifikats
- Format: Textdatei; ein abschließender Zeilenumbruch ist zulässig
- Besitzer auf Linux: `APP_UID:APP_GID`
- Rechte auf Linux: `600`

Sichere Prüfung ohne Inhaltsausgabe:

```sh
test -s secrets/data_protection_certificate_password
```

### `proxy_guard_secret`

**Nur bei `PROXY_GUARD_ENABLED=true` erforderlich.**

Die Datei muss exakt 64 kleine Hex-Zeichen enthalten; ein abschließender Zeilenumbruch ist zulässig.

Sichere Erzeugung ohne Ausgabe auf dem Terminal:

```sh
umask 077
openssl rand -hex 32 > secrets/proxy_guard_secret
```

- Ablage: `<Runtime-Root>/secrets/proxy_guard_secret`
- Zielpfad im Container: `/run/secrets/proxy_guard_secret`
- HTTP-Header: `X-WebUI-Proxy-Guard`
- Besitzer auf Linux: `APP_UID:APP_GID`
- Rechte auf Linux: `600`

Sichere Strukturprüfung ohne Ausgabe des Secretwerts:

```sh
LC_ALL=C grep -Eq '^[0-9a-f]{64}$' secrets/proxy_guard_secret
```

Der Reverse Proxy muss bei aktiviertem Guard für jede an die WebUI weitergeleitete Anfrage genau diesen Header mit demselben Secretwert setzen. Der Wert darf nicht in URLs, Diagnoseausgaben oder Screenshots erscheinen.

## 7. Persistente Schreibpfade

Der Container läuft mit `read_only: true`. Dauerhaft schreibbar sind ausschließlich:

```text
Host                              Container
navigation-cache/                 /data/navigation-cache
diagnostic/oidc/                  /data/oidc-diagnostics
diagnostic/performance/           /data/performance-diagnostics
user-tokens/                      /data/user-tokens
data-protection-keys/             /data/data-protection-keys
```

Zusätzlich wird die Host-Konfiguration read-only eingebunden:

```text
Host                              Container
config/                           /data/config
```

Damit bestehen insgesamt sieben Bind-Mounts: fünf persistente RW-Pfade sowie zwei read-only Bind-Mounts für `secrets/` und `config/`.

`navigation-cache/` enthält regenerierbare Navigationsdaten.

`user-tokens/` enthält die geschützten persönlichen Paperless-Zuordnungen der WebUI-Benutzer.

`data-protection-keys/` enthält die ASP.NET-Core-Data-Protection-Schlüssel. Diese Schlüssel, das Data-Protection-Zertifikat und dessen Kennwort müssen bei einem Erhalt bestehender geschützter Benutzertokens zusammen erhalten bleiben.

Die Diagnoseverzeichnisse enthalten eigene OIDC- bzw. Performance-Diagnosedateien. Beide sind in der Compose mit `RetentionDays=7` konfiguriert.

Auf Linux ist für alle fünf persistenten Verzeichnisse Modus `700` und als Zielbesitzer `APP_UID:APP_GID` vorgesehen. Geschützte Token- und Key-Dateien sind mit Modus `600` zu halten. Die tatsächliche Schreibbarkeit aller fünf Pfade muss vor Freigabe praktisch mit dem Containerbenutzer geprüft werden.

## 8. Docker-Netzwerk

Es gibt keinen eigenen `networks:`-Abschnitt.

Compose erzeugt für das Projekt sein normales Default-Bridge-Netz. Dieses Netz ist von anderen Compose-Projekten getrennt, ohne einen zusätzlichen frei gewählten Netzwerknamen einzuführen.

Die WebUI wird nicht standardmäßig an ein Paperless-ngx-Docker-Netz angehängt. `PAPERLESS_BASE_URL`, `OIDC_AUTHORITY` und `OIDC_METADATA_ADDRESS` müssen über reguläres Routing aus dem Container erreichbar sein.

`internal: true` wird nicht verwendet, weil die WebUI Paperless-ngx und den OIDC-Provider erreichen können muss.

## 9. Compose-Projektname

Der Compose-Projektname wird bei der Installation eindeutig vergeben. Haupt- und Rückfallprojekt derselben Version müssen unterscheidbare Namen erhalten.

Empfohlenes Schema:

```text
webui-<Version>-main
webui-<Version>-rueckfall
```

Beispielsweise:

```text
webui-vXYZ-main
webui-vXYZ-rueckfall
```

Bei Docker Compose kann der Projektname mit `-p` gesetzt werden. In einer grafischen Containerverwaltung wird der entsprechende Projektname dort eingetragen.

Der Projektname beeinflusst unter anderem den von Compose erzeugten Namen des Default-Netzwerks. Haupt- und Rückfallprojekt dürfen wegen identischer Host-Portbindung nicht gleichzeitig gestartet werden.

## 10. Reverse Proxy und Forwarded Headers

Der normale Zugriff erfolgt über einen Reverse Proxy mit HTTPS.

Der Reverse Proxy leitet an:

```text
<WEBUI_BIND_ADDRESS>:<WEBUI_PORT>
```

weiter und setzt:

- `X-Forwarded-For`
- `X-Forwarded-Proto`

`X-Forwarded-Host` wird von der WebUI nicht ausgewertet.

Die Anwendung vertraut nicht pauschal ganzen privaten Netzen. Unter Linux ermittelt sie genau ein Docker-Default-Gateway und verwendet dieses als unmittelbaren `KnownProxy`. Fehlt ein eindeutiges gültiges Default-Gateway, beendet die Anwendung den Start sicher.

Die breite ASP.NET-Core-Automatik über `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` darf nicht aktiviert werden.

## 11. Proxy Guard

Der Proxy Guard ist eine zusätzliche Anwendungsbarriere vor der Forwarded-Headers-Verarbeitung.

Bei aktiviertem Guard:

1. muss die Guard-Secretdatei beim Start gültig lesbar sein;
2. jede Anfrage muss `X-WebUI-Proxy-Guard` mit dem korrekten Wert enthalten;
3. fehlender oder falscher Header führt zu HTTP `403`;
4. ein gültiger Guard-Header wird vor der weiteren Verarbeitung entfernt;
5. danach werden die zulässigen Forwarded Headers verarbeitet.

Empfehlung:

- Reverse Proxy auf demselben Host und Host-Bindung auf `127.0.0.1`: Guard optional.
- Reverse Proxy auf einem anderen Rechner: Guard zusätzlich zur Netz-/Firewallbeschränkung verwenden.

Der Proxy Guard ersetzt keine Firewall.

## 12. Healthcheck

Der Healthcheck prüft alle 30 Sekunden den lokalen Zustand der WebUI:

```text
interval:     30s
timeout:       8s
retries:       3
start_period: 30s
```

Guard deaktiviert:

```text
GET http://127.0.0.1:8080/healthz
```

Guard aktiviert:

```text
dotnet WebUI.Web.dll --proxy-guard-healthcheck
```

Der interne Guard-Healthcheck liest das Secret selbst und gibt es nicht aus.

Nach drei aufeinanderfolgenden fehlgeschlagenen Prüfungen wird der Container als `unhealthy` markiert. `restart: unless-stopped` startet einen weiterhin laufenden Prozess allein wegen `unhealthy` nicht neu. Erholt sich der Healthcheck, kann der Status wieder `healthy` werden.

`/healthz` wird nicht vom Proxy Guard ausgenommen.

## 13. Sicherheitsparameter

Die Compose verwendet:

```text
read_only: true
init: true
security_opt:
  - no-new-privileges:true
cap_drop:
  - ALL
```

Es werden keine Linux-Capabilities wieder hinzugefügt.

`/tmp` ist als beschränktes RAM-Dateisystem verfügbar:

```text
/tmp:size=64m,mode=1777
```

Die 64 MiB werden nicht beim Start vollständig reserviert. Der Speicher wächst nur mit tatsächlicher Nutzung und zählt zum Container-Arbeitsspeicher.

## 14. Ressourcen

```text
mem_limit: 1g
mem_reservation: 256m
```

`mem_limit` ist die harte Obergrenze. `mem_reservation` ist eine weiche Speicherreservierung.

Es wird kein CPU-Limit gesetzt.

## 15. Docker-Logging und Diagnosen

Docker verwendet:

```text
driver: json-file
max-size: 10m
max-file: 3
```

Damit werden die Docker-Standardausgaben begrenzt rotiert. Diese Rotation ist unabhängig von:

```text
diagnostic/oidc/
diagnostic/performance/
```

Die eigenen Diagnosedateien werden durch die Anwendung separat behandelt.

Die optionale Display-Präfix-Konfiguration wird hostseitig unter `config/display-prefixes.json` abgelegt und als read-only Bind-Mount nach `/data/config` eingebunden. Die Anwendung liest sie über `/data/config/display-prefixes.json`; der Container darf diesen Pfad nicht beschreiben.

Secretwerte dürfen weder in Docker-Logs noch in den Diagnosedateien ausgegeben werden.

## 16. Startvorbereitung

Vor dem ersten Start müssen vorhanden sein:

```text
compose.yaml
.env
config/
secrets/
navigation-cache/
diagnostic/oidc/
diagnostic/performance/
user-tokens/
data-protection-keys/
releases/
```

`config/display-prefixes.json` ist optional. Wenn die Datei vorhanden ist, wird sie vom Container ausschließlich read-only über `/data/config/display-prefixes.json` gelesen.

Für das Hauptprojekt liegt `compose.yaml` zusammen mit `.env` direkt im Runtime-Root und `.env` enthält `WEBUI_RUNTIME_ROOT=.`. Für ein Rückfallprojekt liegen beide Dateien unter `config/<Version>/` und `.env` enthält `WEBUI_RUNTIME_ROOT=../..`. Die `compose.yaml` ist in beiden Fällen byteidentisch.

Vor einem Start ist zu prüfen:

- `.env` enthält alle Pflichtwerte;
- `oidc_client_secret`, `data_protection_certificate` und `data_protection_certificate_password` sind vorhanden; `proxy_guard_secret` ist nur bei aktiviertem Guard erforderlich;
- alle schreibbaren Verzeichnisse sind für `APP_UID:APP_GID` zugänglich;
- das in `WEBUI_IMAGE` angegebene Image ist lokal vorhanden;
- Host-Port und Bind-Adresse sind korrekt;
- Paperless-ngx und OIDC sind aus dem Container erreichbar.

## 17. Start und Statusprüfung

Der freigegebene Standardstartweg führt zwingend durch `start-container.sh`. Der Wrapper lädt die nicht geheime `.env`, führt `validate-runtime-config.sh` aus und ruft `docker compose up -d` nur bei erfolgreicher UID/GID-Prüfung auf.

Beispiel für ein Hauptprojekt:

```sh
./start-container.sh webui-vXYZ-main
```

Ein direkter Aufruf von `docker compose ... up -d` umgeht diese vorgeschaltete UID/GID-Prüfung und ist deshalb nicht der freigegebene Standardstartweg.

Ein Start darf erst erfolgen, wenn der konkrete Image-, Compose- und Konfigurationsstand für die Zielumgebung freigegeben ist.

Der Wrapper gibt keine `.env`-Inhalte aus und liest keine Secret-Dateien. Der erwartete Endzustand ist ein laufender Container mit Health-Status `healthy`.

## 18. Funktionsprüfung

Nach dem Start sind mindestens zu prüfen:

- Health-Status `healthy`;
- HTTPS-Aufruf über den Reverse Proxy;
- OIDC-Anmeldung;
- erfolgreiche persönliche Paperless-Verbindung;
- Navigation und Dokumentanzeige;
- Schreiben/Aktualisieren des Navigation-Cache;
- Lesen/Schreiben der geschützten Benutzertokens;
- Lesen/Schreiben der Data-Protection-Keys;
- OIDC- und Performance-Diagnosepfade;
- bei aktiviertem Guard: Anfrage ohne oder mit falschem Guard-Header wird mit `403` abgewiesen;
- Forwarded-Proto wird als `https` verarbeitet;
- WebUI führt keine schreibenden Paperless-API-Operationen aus.

## 19. Update

Ein Update wird erst durchgeführt, wenn das neue Image und die neue byteidentische `compose.yaml` für die Zielarchitektur vollständig geprüft und als Releasebestand archiviert sind.

Verbindliche Reihenfolge:

1. neues Image für die Zielarchitektur bauen und prüfen;
2. Image-TAR, dessen SHA-256 und den zugehörigen Image-Nachweis erzeugen;
3. `compose.yaml` statisch prüfen und mit SHA-256 sichern;
4. vollständige `.env` für das Hauptprojekt mit `WEBUI_RUNTIME_ROOT=.` und für das Rückfallprojekt mit `WEBUI_RUNTIME_ROOT=../..` erzeugen und jeweils mit SHA-256 sichern;
5. Releaseverzeichnis `releases/<Version>/` vollständig anlegen;
6. versionsbezogenen Rückfallbestand unter `config/<Version>/` mit derselben `compose.yaml` und der Rückfall-`.env` bereitstellen;
7. bisheriges Hauptprojekt kontrolliert stoppen;
8. neues Hauptprojekt aus dem Runtime-Root mit der Haupt-`.env` anlegen und starten;
9. Healthcheck, Reverse Proxy, OIDC, Paperless-Verbindung, Mount-Auflösung und Persistenz prüfen;
10. erst nach bestandener Prüfung den neuen Stand als freigegebenen Betriebsstand dokumentieren.

Historische Releaseartefakte und persistente Runtime-Daten werden nicht gelöscht. Ein registriertes älteres Container-Manager-Projekt muss nicht dauerhaft erhalten bleiben, wenn der zugehörige Release- und Rückfallbestand revisionssicher vorhanden ist.

## 20. Releasearchiv

Für jeden freigegebenen Stand wird ein eigenes Verzeichnis verwendet:

```text
releases/<Version>/
├── compose.yaml
├── compose.yaml.sha256
├── .env-hauptverzeichnis
├── .env-hauptverzeichnis.sha256
├── .env-config-version
├── .env-config-version.sha256
├── image-arm64.tar
├── image-arm64.tar.sha256
├── image-arm64-digest.txt
├── image-amd64.tar
├── image-amd64.tar.sha256
└── image-amd64-digest.txt
```

Nur die für den jeweiligen Release tatsächlich erzeugten und geprüften Architekturartefakte werden aufgenommen.

Die beiden `.env`-Dateien enthalten ausschließlich nicht geheime installations- und releasebezogene Werte. Secretwerte bleiben weiterhin außerhalb des Releasearchivs.

Die Varianten unterscheiden sich hinsichtlich des Projektpfads:

```text
.env-hauptverzeichnis
WEBUI_RUNTIME_ROOT=.

.env-config-version
WEBUI_RUNTIME_ROOT=../..
```

Nicht in das Releasearchiv gehören:

- Secret-Dateien;
- persönliche Tokens;
- Data-Protection-Keys;
- Navigation-Cache;
- Diagnose-Logs.

Die aktive `compose.yaml`, die Rückfall-`compose.yaml` und die archivierte `compose.yaml` müssen byteidentisch sein.

Image-TAR, SHA-256 und Image-Digest sind getrennte Nachweise und dürfen nicht miteinander gleichgesetzt werden.

## 21. Rollback

Ein Rollback erfolgt ausschließlich auf einen dokumentierten und bereits geprüften Rückfallstand.

Verbindliche Reihenfolge:

1. Zielversion des Rückfallstands eindeutig festlegen.
2. SHA-256 der archivierten `compose.yaml` prüfen.
3. SHA-256 der archivierten `.env-config-version` prüfen.
4. SHA-256 des zur Zielarchitektur gehörenden Image-TAR prüfen.
5. zugehörigen Image-Digest-/Image-Nachweis prüfen.
6. sicherstellen, dass Secrets und persistente Runtime-Daten weiterhin vorhanden sind.
7. laufendes Hauptprojekt kontrolliert stoppen.
8. Rückfallimage aus dem geprüften Image-TAR bereitstellen bzw. laden, falls es lokal nicht mehr vorhanden ist.
9. unter `config/<Version>/` die byteidentische `compose.yaml` und die geprüfte Rückfall-`.env` mit `WEBUI_RUNTIME_ROOT=../..` bereitstellen.
10. Rückfallprojekt mit eindeutigem Rückfall-Projektnamen anlegen und starten.
11. Healthcheck und tatsächliche Bind-Mount-Quellpfade prüfen.
12. HTTPS-Zugriff über Reverse Proxy prüfen.
13. OIDC-Anmeldung prüfen.
14. persönliche Paperless-Verbindung prüfen.
15. Navigation, Benutzertokens, Data-Protection-Keys und Diagnosepfade prüfen.
16. erst nach erfolgreicher Prüfung den Rückfall als abgeschlossen dokumentieren.

Das Hauptprojekt und das Rückfallprojekt derselben Version dürfen wegen identischer Host-Portbindung nicht gleichzeitig gestartet werden.

Ein Rollback darf keine historischen Laufzeit-, Diagnose- oder Releasebestände stillschweigend löschen.


## 22. Fehler- und Abbruchregeln

Bei einem fehlenden Pflichtwert, fehlendem Secret, nicht erreichbarem OIDC/Paperless-Ziel, ungültigem Proxy-Guard-Secret, nicht beschreibbarem Persistenzpfad oder fehlgeschlagenem Healthcheck wird nicht durch unsichere Ersatzwerte weitergearbeitet.

Insbesondere nicht vorgesehen sind:

- Secrets direkt in `.env` oder Compose;
- `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true`;
- pauschales Vertrauen in ein privates Subnetz;
- Docker-Socket-Zugriff;
- Rootbetrieb als Normalweg;
- `chmod 777`;
- pauschales `chown -R`;
- freies Exponieren des Backend-Ports als Standard;
- automatisches Löschen alter Release- oder Rückfallbestände.
