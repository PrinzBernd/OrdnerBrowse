# Mitwirken

## Grundsätze

Beiträge zu OrdnerBrowse sollen klein, nachvollziehbar und auf einen klar abgegrenzten fachlichen oder technischen Zweck begrenzt sein.

Änderungen sollen sich am bestehenden Projektverhalten, der Architektur und der öffentlichen Projektdokumentation orientieren.

Vor einer Änderung ist zu prüfen, ob sie mit den bestehenden Architektur-, Sicherheits-, Read-only- und Veröffentlichungsregeln des Projekts vereinbar ist.

Nicht belegte Annahmen über das gewünschte Verhalten sollen nicht zur Grundlage einer Änderung gemacht werden. Ist das beabsichtigte Verhalten unklar, sollte die fachliche oder technische Zielsetzung zunächst geklärt werden.

## Zulässiger Änderungsumfang

Änderungen sollen möglichst ursachenorientiert und minimalinvasiv umgesetzt werden. Sie sollen sich auf den tatsächlich betroffenen fachlichen oder technischen Bereich beschränken.

Nicht zusammenhängende Änderungen, größere Umbauten oder zusätzliche Funktionsänderungen sollten getrennt behandelt werden.

Bestehendes Verhalten außerhalb des beabsichtigten Änderungsumfangs soll nicht ohne nachvollziehbaren Grund verändert werden.

Eine Änderung sollte nur die Dateien und Komponenten betreffen, die für ihren Zweck tatsächlich erforderlich sind.

## Read-only-Garantie

OrdnerBrowse muss fachlich read-only gegenüber paperless-ngx bleiben.

Insbesondere dürfen keine schreibenden Paperless-API-Methoden für Dokumente, Metadaten, Benutzer oder Rechte eingeführt werden.

paperless-ngx bleibt die alleinige Daten-, Dokumenten-, Benutzer- und Rechtequelle.

Änderungen an lokalen Sitzungs-, Cache-, Diagnose- oder sonstigen technischen Laufzeitdaten von OrdnerBrowse sind davon getrennt zu betrachten. Sie dürfen die fachliche Read-only-Grenze gegenüber paperless-ngx nicht aufheben oder umgehen.

## Secrets und Echtdaten

Beiträge dürfen keine Kennwörter, API-Tokens, 2FA-Codes, Client-Secrets, sonstigen Secret-Inhalte, privaten Schlüssel, Originaldokumente, produktiven Konfigurations- oder Laufzeitdateien, personenbezogenen Echtdaten oder sonstigen vertraulichen Inhalte enthalten.

Das gilt auch für Quellcode, Tests, Beispieldateien, Konfigurationsvorlagen, Protokollauszüge, Screenshots und sonstige mit einem Beitrag übermittelte Dateien.

Beispiele, Tests und Dokumentation müssen ausschließlich neutrale beziehungsweise vollständig neutralisierte Platzhalter und Beispieldaten verwenden.

Besteht Unsicherheit, ob ein Inhalt vertraulich oder produktiv ist, soll er nicht in einen Beitrag aufgenommen werden.

## Tests

Änderungen sollen durch geeignete Tests abgesichert werden.

Bestehende automatisierte Tests müssen weiterhin erfolgreich durchlaufen. Wird neues oder geändertes Verhalten eingeführt, sollen die dazugehörigen Tests ergänzt oder angepasst werden, soweit das Verhalten automatisiert prüfbar ist.

Tests sollen den tatsächlich geänderten Funktionsumfang abdecken und insbesondere sicherstellen, dass bestehendes Verhalten außerhalb des beabsichtigten Änderungsumfangs nicht unbeabsichtigt verändert wird.

Änderungen an der Anbindung an paperless-ngx müssen zusätzlich die fachliche Read-only-Grenze des Projekts berücksichtigen und dürfen keine schreibenden Paperless-API-Zugriffe voraussetzen oder einführen.

Ein erfolgreicher Testlauf allein bedeutet noch keine formale Freigabe oder Veröffentlichung einer Version.

## Versionen und Releases

Versionsnummern und Release-Status dürfen nicht allein aufgrund einer Quelländerung angehoben oder als freigegeben bezeichnet werden.

Eine höhere Versionsnummer, ein erfolgreicher Build oder ein bestandener Testlauf bedeutet für sich allein noch keine formale Freigabe.

Release Candidates, geänderte Arbeitsstände und andere noch nicht vollständig freigegebene Stände müssen entsprechend als solche erkennbar bleiben.

Eine stabile Version gilt erst dann als formal freigegeben, wenn die dafür vorgesehenen Prüf- und Freigabeschritte vollständig abgeschlossen und der Freigabestatus ausdrücklich dokumentiert wurden.

Beiträge dürfen daher weder einen noch nicht freigegebenen Stand als Release darstellen noch den dokumentierten Freigabeprozess durch eine reine Versionsänderung vorwegnehmen.

## Drittanbieter-Code

Neue oder geänderte Drittanbieter-Abhängigkeiten müssen hinsichtlich Herkunft, Version, Lizenz, Weiterverteilbarkeit und erforderlicher Lizenzhinweise geprüft werden.

Es dürfen nur Abhängigkeiten aufgenommen werden, deren Nutzung und Weiterverteilung mit der Projektlizenz und der vorgesehenen Veröffentlichung vereinbar ist.

Erforderliche Lizenzhinweise und Lizenztexte müssen zusammen mit der jeweiligen Änderung vollständig und nachvollziehbar aktualisiert werden.

Soweit eine Änderung Auswirkungen auf `THIRD_PARTY_NOTICES.md` oder die unter `LICENSES/` geführten Lizenztexte hat, müssen diese Dateien entsprechend angepasst werden.

Drittanbieter-Code oder fremde Inhalte dürfen nicht ohne geklärte Herkunft und Lizenzbedingungen in das Projekt übernommen werden.

## Lizenzierung von Beiträgen

Beiträge zu OrdnerBrowse müssen mit der Projektlizenz `AGPL-3.0-or-later` vereinbar sein.

Wer einen Beitrag einreicht, muss berechtigt sein, den enthaltenen eigenen Quellcode, die Dokumentation und sonstigen eigenen Inhalte unter Bedingungen beizutragen, die eine Veröffentlichung des Gesamtprojekts unter `AGPL-3.0-or-later` ermöglichen.

Beiträge dürfen keine zusätzlichen Lizenzbedingungen, Nutzungsbeschränkungen oder sonstigen Rechtevorbehalte enthalten, die einer Veröffentlichung oder Weiterverteilung des Gesamtprojekts unter `AGPL-3.0-or-later` entgegenstehen.

Für enthaltene Drittanbieter-Bestandteile gelten zusätzlich die Anforderungen aus dem Abschnitt `Drittanbieter-Code`.
