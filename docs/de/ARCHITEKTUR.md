# Architektur

## Rolle von OrdnerBrowse

OrdnerBrowse ist eine eigenständige, unabhängig entwickelte und inoffizielle Weboberfläche für paperless-ngx.

Die Anwendung stellt vorhandene Dokumente und zugehörige Informationen aus paperless-ngx in einer alternativen, dokumentorientierten Oberfläche dar.

paperless-ngx bleibt dabei die alleinige Daten-, Dokumenten-, Benutzer- und Rechtequelle. Die Datenhaltung und Rechteverwaltung von paperless-ngx werden durch OrdnerBrowse nicht ersetzt.

## Read-only-Prinzip

OrdnerBrowse arbeitet gegenüber paperless-ngx fachlich ausschließlich lesend.

Die Anwendung liest Dokumente, Metadaten, Korrespondenten, Dokumenttypen, Speicherpfade, Tags, benutzerdefinierte Felder, Notizen sowie Benutzer- und Rechteinformationen aus paperless-ngx, verändert diese Daten jedoch nicht.

Schreibende Paperless-API-Methoden für fachliche Daten, Dokumente, Benutzer oder Rechte sind nicht Bestandteil der Architektur und dürfen nicht eingeführt werden.

Lokale Funktionen von OrdnerBrowse, beispielsweise Anmeldung, Sitzungsverwaltung, persönliche Paperless-Verbindungen, Cache oder Diagnose, können eigene Laufzeitdaten verwalten. Sie ändern das fachliche Read-only-Prinzip gegenüber paperless-ngx nicht.

Dieses Prinzip ist eine zentrale Architektur- und Sicherheitsgrenze des Projekts.

## Technische Hauptkomponenten

OrdnerBrowse besteht im Wesentlichen aus drei technischen Projektbereichen:

- `WebUI.Web`: Webanwendung und Benutzeroberfläche. Dieser Bereich stellt die Weboberfläche bereit und enthält die für den Anwendungsbetrieb benötigten Webfunktionen.
- `WebUI.Infrastructure`: technische Infrastruktur sowie die Anbindung an externe Dienste, insbesondere an paperless-ngx.
- `WebUI.Tests`: automatisierte projektbezogene Tests zur Absicherung des vorgesehenen Verhaltens und zur Erkennung von Regressionen.

Die technische Projektbezeichnung `WebUI` wird dort weiterverwendet, wo sie Bestandteil von Quellcode, Projektdateien, Pfaden oder Containerbetrieb ist.

Die öffentliche Produktbezeichnung lautet **OrdnerBrowse**.

## Konfiguration und Laufzeitdaten

OrdnerBrowse trennt öffentlichen Quellcode, nicht geheime Laufzeitkonfiguration, Secrets und persistente Laufzeitdaten voneinander.

Nicht geheime Konfigurationswerte und öffentliche Konfigurationsvorlagen dürfen Bestandteil des Quellstands sein. Dazu gehören beispielsweise neutrale Vorlagen und Einstellungen für die Darstellung.

Produktive Secrets wie API-Tokens, Client-Secrets, Kennwörter oder private Schlüssel gehören nicht in den Quellcode oder das Repository. Sie werden außerhalb des öffentlichen Quellstands über die dafür vorgesehenen geschützten Laufzeitpfade bereitgestellt.

Persistente Laufzeitdaten der Anwendung werden ebenfalls außerhalb des eigentlichen Quellcodes gehalten. Dadurch bleiben Programmstand, Konfiguration, Secrets und zur Laufzeit entstehende beziehungsweise benötigte Daten technisch voneinander getrennt.

Öffentliche Beispiele und Vorlagen verwenden ausschließlich neutrale Platzhalter und dürfen keine produktiven oder vertraulichen Inhalte enthalten.

Die konkreten Konfigurationsmöglichkeiten sind in `docs/de/KONFIGURATION.md` und die betrieblichen Laufzeitpfade in `Containerbetrieb/README_CONTAINERBETRIEB.md` beschrieben.

## Containerbetrieb

OrdnerBrowse ist für den Betrieb als Containeranwendung vorgesehen.

Das Containerimage enthält die Anwendung und die für ihren Betrieb erforderlichen Programmkomponenten. Installationsabhängige Konfiguration, Secrets und persistente Laufzeitdaten werden nicht fest in das Image eingebaut, sondern zur Laufzeit über die dafür vorgesehenen externen Pfade beziehungsweise Einbindungen bereitgestellt.

Dadurch kann derselbe Anwendungsstand mit unterschiedlichen installationsspezifischen Konfigurationen betrieben werden, ohne den eigentlichen Quell- oder Programmstand zu verändern.

Der Containerbetrieb wird für zwei CPU-Architekturen berücksichtigt:

- **ARM64** für die lokale ARM64-Referenzumgebung,
- **AMD64** für die DiskStation-Referenzumgebung.

Für beide Architekturen wird derselbe Quellstand verwendet. Die jeweiligen Containerimages werden jedoch architekturspezifisch gebaut und geprüft.

Der Container stellt einen eigenen Healthcheck bereit, über den der technische Zustand der laufenden Anwendung geprüft werden kann.

Die konkrete Containerstruktur, Konfigurationsparameter, Einbindungen, Persistenz sowie die vorgesehenen Build-, Start- und Prüfabläufe sind ausführlich dokumentiert in:

`Containerbetrieb/README_CONTAINERBETRIEB.md`

## OIDC und Reverse Proxy

Für die Anmeldung im regulären Betrieb verwendet OrdnerBrowse OpenID Connect (OIDC).

Die Authentifizierung erfolgt über einen externen OIDC-Anbieter. OrdnerBrowse übernimmt die für die Anmeldung erforderliche OIDC-Kommunikation, verwaltet jedoch keine eigene zentrale Benutzer- oder Rechtequelle.

Benutzer- und Rechteinformationen für den Zugriff auf Dokumente stammen weiterhin aus paperless-ngx.

Im vorgesehenen Produktivbetrieb kann OrdnerBrowse hinter einem Reverse Proxy betrieben werden. Der Reverse Proxy übernimmt dabei die externe Bereitstellung der Anwendung und kann insbesondere HTTPS-Terminierung, Weiterleitung und Hostnamen-Zuordnung übernehmen.

OIDC-Anbieter, Reverse Proxy und OrdnerBrowse bleiben technisch getrennte Komponenten mit jeweils eigener Aufgabe.

Für den lokalen Entwicklungs- und Testbetrieb bestehen davon getrennte Anmeldemechanismen, die ausschließlich für die lokale Entwicklungsumgebung vorgesehen sind.
