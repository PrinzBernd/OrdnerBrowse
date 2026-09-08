# Änderungsverlauf

Dieses Changelog beschreibt die öffentlich veröffentlichten Versionen von
OrdnerBrowse und deren öffentlich relevante Änderungen.

Interne Entwicklungsstände, lokale Prüfstände und Versionen vor der ersten
öffentlichen Veröffentlichung werden hier nicht als Releases geführt.

## v09.91.1 – Erste öffentliche Beta-Veröffentlichung

### Funktionen

- Eigenständige, inoffizielle und dokumentorientierte WebUI für paperless-ngx
  mit ausschließlich lesendem Zugriff.
- Mehrstufige Navigation durch Bereiche beziehungsweise Speicherpfade,
  Korrespondenten, Dokumenttypen und Dokumente.
- Filterung von Korrespondenten und Dokumenttypen innerhalb des gewählten
  Navigationsbereichs.
- Suche nach Begriffen in Dokumenttiteln mit Unterstützung von UND (`&`) und
  ODER (`|`) für mehrere Suchbegriffe.
- Sortierung von Bereichen, Korrespondenten, Dokumenttypen und Dokumenten.
- Dokumentvorschau über das von paperless-ngx bereitgestellte Thumbnail mit
  vertikal scrollbar zugänglichem Detailbereich.
- Mehrseitige PDF-Schnellansicht direkt in OrdnerBrowse.
- Anzeige wichtiger Dokumentinformationen wie Tags, benutzerdefinierter Felder,
  Metadaten und der in paperless-ngx hinterlegten Dokumentnotizen einschließlich
  Benutzerangabe und Datum sowohl in der Dokumentvorschau als auch in der
  PDF-Schnellansicht.
- Direkter Wechsel zum ausgewählten Dokument im paperless-ngx-Frontend.
- Anpassbare Oberfläche mit veränderbaren beziehungsweise einklappbaren Spalten
  sowie verschiebbarer und größenveränderbarer PDF-Schnellansicht.
- Extern konfigurierbare Anzeigepräfixe für Bereiche beziehungsweise
  Speicherpfade, Korrespondenten, Dokumenttypen und Dokumenttitel.
- Unterstützung fester Anzeigepräfixe, definierter Datumsformate sowie frei kombinierbarer `?`-/`#`-Zeichenmuster.
- Gültige Änderungen der Anzeigepräfix-Konfiguration können zur Laufzeit
  übernommen werden, ohne die zugrunde liegenden Bezeichnungen in
  paperless-ngx zu verändern.

### Betrieb und Konfiguration

- Containerbetrieb für ARM64 und AMD64 auf gemeinsamer Quellcodebasis
  vorbereitet.
- Dockerfile, Compose-Vorlage und Hilfen für Konfiguration, Validierung und
  kontrollierten Containerstart bereitgestellt.
- Laufzeitkonfiguration, Secrets und persistente Daten werden vom öffentlichen
  Quellcode getrennt gehalten.
- Öffentliche Konfigurationsbeispiele verwenden ausschließlich neutrale
  Platzhalter.
- Anzeigepräfixe werden über eine externe JSON-Konfiguration bereitgestellt.
- Für den regulären Betrieb wird OpenID Connect (OIDC) zur Anmeldung verwendet.
- Lokaler Entwicklungs- und Testbetrieb ist von der regulären
  OIDC-Anmeldung getrennt.
- Healthcheck sowie OIDC- und Performance-Diagnose sind für den
  Containerbetrieb dokumentiert.
- Container-Images werden nicht automatisch aus einer Registry geladen;
  der dokumentierte Betrieb verwendet `pull_policy: never`.

### Sicherheit und Datenschutz

- OrdnerBrowse arbeitet gegenüber paperless-ngx fachlich ausschließlich
  lesend.
- paperless-ngx bleibt die alleinige Daten-, Dokumenten-, Benutzer- und
  Rechtequelle.
- Schreibende Paperless-API-Methoden für Dokumente, Metadaten, Benutzer oder
  Rechte gehören nicht zum Funktionsumfang.
- Persönliche Paperless-Verbindungen und sicherheitsrelevante Laufzeitdaten
  werden serverseitig beziehungsweise außerhalb des öffentlichen Quellcodes
  verarbeitet.
- Kennwörter, API-Tokens, 2FA-Codes, Client-Secrets, private Schlüssel,
  Originaldokumente und produktive Laufzeitdateien gehören nicht in den
  öffentlichen Quellstand oder öffentliche Projektartefakte.
- Sicherheits-, Support- und Beitragsdokumentation beschreibt die vorgesehenen
  Grenzen für Secrets, Echtdaten und produktive Informationen.

### Dokumentation und Lizenzierung

- OrdnerBrowse wird unter `AGPL-3.0-or-later` veröffentlicht.
- Drittanbieter-Komponenten und ihre Lizenzbedingungen werden in
  `THIRD_PARTY_NOTICES.md` dokumentiert.
- Erforderliche Drittanbieter-Lizenztexte werden unter `LICENSES/` mitgeführt.
- Öffentliche Dokumentation für Architektur, Konfiguration, Containerbetrieb,
  Sicherheit, Support und Beiträge ist Bestandteil des Projekts.
- Dokumentation für die externe Anzeigepräfix-Konfiguration einschließlich
  neutraler Beispielkonfiguration ist enthalten.
