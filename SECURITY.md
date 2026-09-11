# Security

## Sicherheitsgrundsätze

OrdnerBrowse arbeitet gegenüber paperless-ngx fachlich ausschließlich lesend.

paperless-ngx bleibt die alleinige Daten-, Dokumenten-, Benutzer- und Rechtequelle. Schreibende Paperless-API-Methoden für fachliche Daten, Dokumente, Benutzer oder Rechte sind nicht Bestandteil der Architektur und dürfen nicht eingeführt werden.

Secrets, persönliche Zugangsdaten und produktive Laufzeitdaten werden vom öffentlichen Quellcode und von öffentlichen Projektartefakten getrennt gehalten.

Kennwörter, API-Tokens, Client-Secrets, 2FA-Codes, private Schlüssel und vergleichbare vertrauliche Werte dürfen nicht in den öffentlichen Quellstand, öffentliche Konfigurationsdateien, Protokolle oder Veröffentlichungsartefakte aufgenommen werden.

Öffentliche Beispiele, Tests und Dokumentation verwenden ausschließlich neutrale beziehungsweise neutralisierte Werte und dürfen keine vertraulichen Echtdaten enthalten.

Lokale Funktionen von OrdnerBrowse, beispielsweise Anmeldung, Sitzungsverwaltung, persönliche Paperless-Verbindungen, Cache oder Diagnose, können eigene Laufzeitdaten verwalten. Sie ändern das fachliche Read-only-Prinzip gegenüber paperless-ngx nicht.

## Sicherheitsproblem melden

Vermutete Sicherheitsprobleme oder Schwachstellen sollen so knapp und reproduzierbar wie möglich beschrieben werden.

Vertrauliche Details einer vermuteten Schwachstelle sollen nicht öffentlich in Issues, Diskussionen oder anderen öffentlich einsehbaren Bereichen veröffentlicht werden.

Für eine Meldung dürfen ausschließlich neutrale beziehungsweise neutralisierte Beispielwerte verwendet werden. Kennwörter, API-Tokens, 2FA-Codes, Secret-Inhalte, private Schlüssel, Originaldokumente, produktive Laufzeitdateien oder andere vertrauliche Echtdaten dürfen nicht übermittelt werden.

Für vertrauliche Sicherheitsmeldungen ist im öffentlichen OrdnerBrowse-Repository GitHub Private Vulnerability Reporting aktiviert. Vermutete Schwachstellen, die vertraulich behandelt werden müssen, sollen über diesen privaten GitHub-Meldeweg gemeldet werden.

Zusätzliche Sicherheitskontakte oder andere private Meldewege werden in dieser Datei nur dann genannt, wenn sie tatsächlich eingerichtet, erreichbar und im öffentlichen Repository dokumentiert sind.

## Benötigte Informationen

Eine Sicherheitsmeldung sollte – soweit ohne vertrauliche Inhalte möglich – die folgenden Angaben enthalten:

- die betroffene OrdnerBrowse-Version,
- die betroffene Funktion oder den betroffenen Anwendungsbereich,
- eine kurze Beschreibung des vermuteten Sicherheitsproblems,
- nachvollziehbare Schritte, mit denen sich das Verhalten reproduzieren lässt,
- das erwartete und das tatsächlich beobachtete Verhalten,
- die beobachtete oder vermutete Sicherheitsauswirkung,
- soweit sicher möglich, eine technische Einordnung des Problems.

Alle Angaben müssen so weit neutralisiert werden, dass keine Kennwörter, Tokens, Secrets, privaten Schlüssel, personenbezogenen Daten, Originaldokumente oder produktiven Laufzeitdaten enthalten sind.

Protokollauszüge, Konfigurationsbeispiele oder Screenshots dürfen nur verwendet werden, wenn sie zuvor vollständig von vertraulichen und produktiven Informationen bereinigt wurden.

Für die Untersuchung soll grundsätzlich nur die kleinste Informationsmenge übermittelt werden, die zur nachvollziehbaren Beschreibung des Problems erforderlich ist.

## Nicht zulässige Inhalte

Unabhängig vom verwendeten Meldeweg dürfen vertrauliche oder produktive Inhalte weder übermittelt noch veröffentlicht werden.

Dazu gehören insbesondere:

- Kennwörter, API-Tokens und 2FA-Codes,
- Client-Secrets, sonstige Secret-Inhalte und private Schlüssel,
- Originaldokumente oder daraus übernommene vertrauliche Inhalte,
- produktive Konfigurations- und Laufzeitdateien,
- personenbezogene oder sonstige vertrauliche Echtdaten,
- nicht bereinigte Protokolle, Screenshots oder Konfigurationsauszüge, die solche Informationen enthalten.

Für Beispiele, Reproduktionsschritte und technische Erläuterungen dürfen ausschließlich neutrale beziehungsweise vollständig neutralisierte Ersatzwerte verwendet werden.

Besteht Unsicherheit, ob eine Information vertraulich oder produktiv ist, soll sie nicht übermittelt werden.

## Read-only- und Secret-Grenzen

paperless-ngx bleibt die alleinige Daten-, Dokumenten-, Benutzer- und Rechtequelle für die von OrdnerBrowse dargestellten fachlichen Inhalte.

OrdnerBrowse greift auf diese Informationen ausschließlich lesend zu. Schreibende Paperless-API-Methoden für fachliche Daten, Dokumente, Benutzer oder Rechte sind nicht Bestandteil der vorgesehenen Architektur.

Lokale Zustands- und Laufzeitdaten von OrdnerBrowse, beispielsweise Sitzungsdaten, persönliche Paperless-Verbindungen, Cache oder Diagnosedaten, sind davon getrennt zu betrachten. Sie dürfen das fachliche Read-only-Prinzip gegenüber paperless-ngx nicht umgehen.

OIDC-Secrets und andere vertrauliche Zugangswerte werden außerhalb des öffentlichen Quellcodes und öffentlicher Konfigurationsdateien bereitgestellt.

Secret-Dateien und andere schutzbedürftige Laufzeitdaten müssen über die dafür vorgesehenen geschützten Laufzeitpfade eingebunden und mit geeigneten Zugriffsrechten vor unberechtigtem Zugriff geschützt werden.

Secrets dürfen weder an den Browser weitergegeben noch in Protokollen, Diagnoseausgaben oder öffentlichen Artefakten offengelegt werden.

## Bearbeitungsmodell

OrdnerBrowse wird als Hobby-/Best-Effort-Projekt entwickelt und gepflegt.

Für Sicherheitsmeldungen bestehen keine zugesicherten Reaktionszeiten, Bearbeitungsfristen, Behebungsfristen oder Service-Level.

Eingehende Sicherheitsmeldungen sollen dennoch entsprechend ihrer nachvollziehbaren Auswirkung und Dringlichkeit geprüft und gegenüber gewöhnlichen Fehler- oder Funktionsmeldungen angemessen priorisiert werden.

Eine Meldung oder deren Prüfung bedeutet nicht automatisch, dass eine bestimmte Änderung, Fehlerbehebung oder Veröffentlichung innerhalb eines festgelegten Zeitraums erfolgt.

Falls für die Behebung eines Sicherheitsproblems eine Änderung am Quellcode oder an veröffentlichten Artefakten erforderlich ist, unterliegt diese weiterhin den für das Projekt geltenden Prüf-, Test- und Freigaberegeln.
