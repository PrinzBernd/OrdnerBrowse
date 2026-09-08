# Support

## Supportmodell

OrdnerBrowse wird als Hobby-/Best-Effort-Projekt entwickelt und gepflegt.

Es bestehen keine zugesicherten Reaktionszeiten, Bearbeitungs- oder Fehlerbehebungsfristen, Wartungsfenster, Verfügbarkeiten oder Service-Level.

Supportanfragen und Fehlermeldungen werden im Rahmen der verfügbaren Zeit geprüft. Daraus entsteht kein Anspruch auf Bearbeitung einer bestimmten Meldung, Umsetzung einer gewünschten Funktion oder Bereitstellung einer Fehlerbehebung innerhalb eines bestimmten Zeitraums.

Sicherheitsprobleme werden gesondert behandelt. Die dafür geltenden Hinweise befinden sich in:

`SECURITY.md`

## Was gemeldet werden kann

Gemeldet werden können insbesondere nachvollziehbare Probleme, die OrdnerBrowse selbst oder die zum Projekt gehörende Dokumentation betreffen.

Dazu gehören beispielsweise:

- reproduzierbare Funktionsfehler in OrdnerBrowse,
- Darstellungs- oder Bedienungsfehler in der Weboberfläche,
- Probleme beim vorgesehenen Containerbetrieb,
- Fehler oder Widersprüche in der öffentlichen Projektdokumentation,
- nachvollziehbare Probleme bei der Konfiguration mit den vom Projekt vorgesehenen Einstellungen und Vorlagen.

Eine Meldung sollte sich möglichst auf ein klar abgrenzbares Problem beziehen und so beschrieben sein, dass das Verhalten mit neutralen Beispielwerten nachvollzogen werden kann.

Wünsche nach neuen Funktionen oder Änderungen können ebenfalls beschrieben werden. Daraus entsteht jedoch kein Anspruch auf Umsetzung oder Aufnahme in eine zukünftige Version.

Vermutete Sicherheitsprobleme gehören nicht in gewöhnliche Supportmeldungen. Dafür gelten die Hinweise in:

`SECURITY.md`

## Hilfreiche Angaben

Eine Supportmeldung sollte – soweit für das jeweilige Problem relevant – möglichst folgende Angaben enthalten:

- die verwendete OrdnerBrowse-Version,
- die betroffene Funktion oder den betroffenen Bereich,
- eine kurze Beschreibung des Problems,
- das erwartete Verhalten,
- das tatsächlich beobachtete Verhalten,
- nachvollziehbare Schritte, mit denen sich das Problem reproduzieren lässt,
- soweit hilfreich, Angaben zur betroffenen Betriebsart oder Zielarchitektur.

Beispiele und Reproduktionsschritte müssen ausschließlich neutrale beziehungsweise neutralisierte Werte verwenden.

Protokollauszüge, Screenshots oder Konfigurationsbeispiele dürfen nur beigefügt werden, wenn personenbezogene, produktive, vertrauliche und geheime Inhalte zuvor vollständig entfernt oder neutralisiert wurden.

Es soll nur die Informationsmenge übermittelt werden, die zur nachvollziehbaren Beschreibung des Problems erforderlich ist.

## Was nicht eingesendet werden darf

Supportmeldungen dürfen keine vertraulichen, produktiven oder geheimen Inhalte enthalten.

Dazu gehören insbesondere:

- Kennwörter, API-Tokens und 2FA-Codes,
- Client-Secrets, sonstige Secret-Inhalte und private Schlüssel,
- Originaldokumente oder daraus übernommene vertrauliche Inhalte,
- produktive Konfigurations- und Laufzeitdateien,
- personenbezogene oder sonstige vertrauliche Echtdaten,
- nicht bereinigte Protokolle, Screenshots oder Konfigurationsauszüge, die solche Informationen enthalten.

Für Beispiele, Reproduktionsschritte und technische Erläuterungen dürfen ausschließlich neutrale beziehungsweise vollständig neutralisierte Ersatzwerte verwendet werden.

Besteht Unsicherheit, ob eine Information vertraulich oder produktiv ist, soll sie nicht eingesendet werden.

## Abgrenzung

Der Support für OrdnerBrowse bezieht sich auf Probleme, die dem Projekt selbst oder seiner bereitgestellten Dokumentation und Konfiguration zugeordnet werden können.

OrdnerBrowse ist für seinen Betrieb auf externe Komponenten und Dienste angewiesen. Probleme in solchen Komponenten können sich auf die Nutzung von OrdnerBrowse auswirken, ohne dass die Ursache in OrdnerBrowse selbst liegt.

Dazu können insbesondere Probleme in folgenden Bereichen gehören:

- paperless-ngx,
- OIDC-Anbieter,
- Reverse Proxy,
- Container-Laufzeit und Hostsystem,
- Netzwerk-, DNS- oder TLS-Konfiguration,
- sonstige eingesetzte Drittanbieter-Komponenten.

Ist die Ursache zunächst nicht eindeutig, kann das Verhalten trotzdem als Supportproblem beschrieben werden. Die Angaben müssen dabei weiterhin den Regeln dieser Datei entsprechen und dürfen keine vertraulichen oder produktiven Inhalte enthalten.

Ergibt die Prüfung, dass die Ursache außerhalb von OrdnerBrowse liegt, kann auf die zuständige Komponente beziehungsweise deren Dokumentation oder Support verwiesen werden. Eine Behebung von Fehlern in externen Projekten oder Diensten kann durch OrdnerBrowse nicht zugesichert werden.

Die Einbindung einer externen Komponente oder die Kompatibilität mit ihr bedeutet nicht, dass OrdnerBrowse diese Komponente selbst entwickelt, betreibt oder unterstützt.
