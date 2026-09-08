# Konfiguration

## Grundprinzip

OrdnerBrowse trennt Quellcode, öffentliche Konfigurationsvorgaben und Vorlagen, installationsabhängige Laufzeitkonfiguration, Secrets sowie persistente Laufzeitdaten voneinander.

Der öffentliche Quellstand darf nicht geheime Standardwerte, Konfigurationsstrukturen und neutrale Beispielvorlagen enthalten. Sie beschreiben die unterstützten Einstellungen, ohne produktive oder vertrauliche Werte vorzugeben.

Installationsabhängige Laufzeitwerte werden entsprechend ihrem Verwendungszweck über die dafür vorgesehenen Konfigurationsdateien, Umgebungswerte oder externen Laufzeitpfade bereitgestellt.

Secrets und andere vertrauliche Werte gehören nicht in den öffentlichen Quellstand oder das Repository. Persistente Laufzeitdaten werden ebenfalls getrennt vom eigentlichen Quellcode gehalten.

Öffentliche Beispiele verwenden ausschließlich neutrale Platzhalter und sind nicht als unmittelbar produktiv einsetzbare Konfiguration gedacht.

## Anzeigepräfixe

Für die Darstellung in OrdnerBrowse können Präfixregeln für Bereiche beziehungsweise Speicherpfade, Korrespondenten, Dokumenttypen und Dokumenttitel konfiguriert werden.

Die Regeln verändern ausschließlich die sichtbare Darstellung in OrdnerBrowse. Die ursprünglichen Bezeichnungen in paperless-ngx bleiben unverändert.

Die Konfiguration verwendet vier Bereiche:

- `Areas`
- `Correspondents`
- `DocumentTypes`
- `Documents`

Unterstützt werden feste Präfixe, definierte Datumsplatzhalter in Jahr- oder Tag-zuerst-Schreibweise sowie frei kombinierbare Zeichenmuster.

In Zeichenmustern steht `?` für genau einen Unicode-Buchstaben oder eine ASCII-Ziffer `0` bis `9`; `#` steht für genau eine ASCII-Ziffer `0` bis `9`. Feste Zeichen können innerhalb eines Musters an der gewünschten Position vorgegeben werden.

Eine Regel wirkt nur am Anfang einer Bezeichnung. Bei mehreren passenden Regeln wird das längste passende Präfix entfernt. Zeichen wie Unterstriche, Bindestriche oder Punkte müssen Bestandteil der Regel sein, wenn sie ebenfalls aus der sichtbaren Darstellung entfernt werden sollen.

Die vollständige Liste der unterstützten Datumsformate, die genaue `?`-/`#`-Syntax, Kombinationsregeln und positive wie negative Beispiele sind ausschließlich in `config/README_display-prefixes.md` detailliert beschrieben.

Ungültige einzelne Regeln werden ignoriert. Ist eine geänderte Konfigurationsdatei insgesamt ungültig, bleibt die zuletzt gültige Konfiguration aktiv.

Fehlt die Konfiguration oder enthält sie keine Regeln, bleiben die Bezeichnungen unverändert.

Die neutrale öffentliche Vorlage befindet sich unter:

`config/display-prefixes.example.json`

Eine ausführliche Beschreibung mit Beispielen befindet sich unter:

`config/README_display-prefixes.md`

Die produktive Konfigurationsdatei liegt außerhalb des öffentlichen Quellstands und wird im Containerbetrieb über den dafür vorgesehenen Laufzeitpfad bereitgestellt.

## Laufzeitpfade

OrdnerBrowse hält installationsabhängige Konfiguration, Secrets und persistente Laufzeitdaten außerhalb des eigentlichen Quellbaums.

Der konkrete Runtime-Root, also das Basisverzeichnis der jeweiligen Laufzeitinstallation, wird installationsabhängig festgelegt. Allgemeine Projektdokumentation verwendet dafür keine konkreten host- oder installationsspezifischen Verzeichnisse.

Innerhalb der Laufzeitumgebung werden unterschiedliche Datenarten voneinander getrennt gehalten. Dazu gehören insbesondere:

- Konfigurationsdateien,
- Secret- und Schutzdateien,
- Navigations- und Cache-Daten,
- benutzerbezogene geschützte Verbindungsdaten,
- Data-Protection-Schlüssel,
- OIDC-Diagnosedaten,
- Performance-Diagnosedaten.

Dabei wird zusätzlich zwischen ausschließlich lesend eingebundenen Bereichen und schreibbaren persistenten Laufzeitbereichen unterschieden. Konfiguration und Secrets werden dem Container nur in der jeweils vorgesehenen Zugriffsart bereitgestellt.

Die konkrete Verzeichnisstruktur, `WEBUI_RUNTIME_ROOT`, die Bind-Mounts und deren Lese-/Schreibrechte sowie die unterschiedlichen Pfadauflösungen für Haupt- und Rückfallbetrieb sind verbindlich dokumentiert in:

`Containerbetrieb/README_CONTAINERBETRIEB.md`

## Secrets und OIDC

Secrets wie API-Tokens, Client-Secrets, Kennwörter oder private Schlüssel dürfen nicht Bestandteil des öffentlichen Quellstands, öffentlicher Konfigurationsdateien oder des Repositorys sein.

Sie werden außerhalb des Quellstands über die dafür vorgesehenen geschützten Laufzeitpfade beziehungsweise Secret-Dateien bereitgestellt.

Für die Anmeldung im regulären Betrieb verwendet OrdnerBrowse OpenID Connect (OIDC). Die hierfür benötigten installationsabhängigen OIDC-Werte werden über die vorgesehene Laufzeitkonfiguration bereitgestellt.

Vertrauliche OIDC-Werte, insbesondere Client-Secrets, werden dabei nicht in öffentlichen Beispiel- oder Projektdateien hinterlegt.

Der OIDC-Anbieter übernimmt die Authentifizierung. Die fachlichen Benutzer- und Rechteinformationen für den Zugriff auf Dokumente stammen weiterhin aus paperless-ngx.

Für den lokalen Entwicklungs- und Testbetrieb bestehen davon getrennte Anmeldemechanismen, die ausschließlich für die lokale Entwicklungsumgebung vorgesehen sind.

Weitere sicherheitsrelevante Hinweise befinden sich in:

`SECURITY.md`

Die konkreten Laufzeit- und Containerparameter sind dokumentiert in:

`Containerbetrieb/README_CONTAINERBETRIEB.md`

## Öffentliche Beispiele und produktive Konfiguration

Öffentliche Beispiel- und Vorlagendateien zeigen die unterstützte Struktur, zulässige Schlüssel und neutrale Beispielwerte einer Konfiguration. Sie dienen als Ausgangspunkt für eine eigene Installation und enthalten keine produktiven oder vertraulichen Werte.

Dazu gehören insbesondere:

- `Containerbetrieb/.env.example` als öffentliche Vorlage für nicht geheime installationsabhängige Container- und Laufzeitparameter,
- `config/display-prefixes.example.json` als neutrale Vorlage für die Konfiguration der Anzeigepräfixe.

Die jeweilige produktive Konfiguration wird installationsbezogen außerhalb des öffentlichen Quellstands beziehungsweise über die dafür vorgesehenen externen Laufzeitpfade bereitgestellt.

Insbesondere eine produktive `.env`-Datei, Secret-Dateien, persönliche Zugangsdaten und andere vertrauliche Laufzeitwerte gehören nicht in das Repository oder einen öffentlichen Quellstand.

Öffentliche Vorlagen dürfen deshalb kopiert und für die eigene Installation angepasst werden; die daraus entstehenden produktiven Dateien bleiben jedoch Bestandteil der jeweiligen Laufzeitumgebung und werden nicht in den öffentlichen Quellstand übernommen.

Änderungen an öffentlichen Vorlagen müssen so gestaltet sein, dass sie weiterhin ausschließlich neutrale und veröffentlichbare Inhalte enthalten.
