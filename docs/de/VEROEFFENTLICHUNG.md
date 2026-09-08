# Veröffentlichung

## Versionsschema

Veröffentlichte Versionen von OrdnerBrowse erhalten eine eindeutig festgelegte Versionsnummer. Eine konkrete Versionsnummer darf erst dann als veröffentlichte Version bezeichnet werden, wenn der dafür vorgesehene Freigabeprozess vollständig abgeschlossen ist.

Release Candidates werden durch das Suffix `-rc.N` gekennzeichnet, wobei `N` die fortlaufende Nummer des Release Candidates bezeichnet.

Beispiel:

`vX.Y.Z-rc.N`

Bereits vorhandene historische Versionsbezeichnungen mit Suffixen wie `-r1` bleiben unverändert und werden nicht nachträglich umbenannt.

Die Nummerierung allein sagt nichts über den Freigabestatus aus. Maßgeblich sind immer der dokumentierte Status und die zugehörigen Nachweise.

## Freigabestatus

Eine Versionsnummer allein begründet keinen Freigabestatus.

Ein Quellstand kann sich beispielsweise in einem geplanten, rekonstruierten, geänderten, ungebauten, getesteten oder als Release Candidate vorbereiteten Zustand befinden, ohne deshalb bereits formal freigegeben zu sein.

Als formal freigegeben gilt eine Version erst dann, wenn alle für diesen Stand vorgesehenen Prüfungen und Freigabeschritte erfolgreich abgeschlossen und die Freigabe ausdrücklich dokumentiert wurden.

Eine öffentliche Veröffentlichung setzt zusätzlich voraus, dass die dafür vorgesehenen Veröffentlichungsbestandteile vollständig und konsistent bereitgestellt wurden.

Release Candidates dienen der abschließenden Prüfung vor einer möglichen stabilen Veröffentlichung und sind nicht mit einer formal veröffentlichten stabilen Version gleichzusetzen.

Maßgeblich sind immer der dokumentierte Status und die zugehörigen Nachweise, nicht allein die Versionsnummer oder der Dateiname eines Artefakts.

## Containerartefakte

Die Build- und Referenzkette von OrdnerBrowse berücksichtigt zwei CPU-Zielarchitekturen:

- **ARM64**
- **AMD64**

Für beide Zielarchitekturen werden Containerartefakte architekturspezifisch gebaut und geprüft. Grundlage ist jeweils derselbe freizugebende Quellstand.

Die erfolgreiche Erstellung und Prüfung eines architekturspezifischen Containerartefakts bedeutet für sich allein noch nicht, dass dieses Artefakt öffentlich veröffentlicht oder über eine Container-Registry bereitgestellt wird.

Eine öffentliche Registry-Strategie und die konkrete Form der Bereitstellung von Containerartefakten werden gesondert festgelegt.

Ein gemeinsames **Multi-Arch-Image**, das mehrere CPU-Architekturen unter einer gemeinsamen Image-Bezeichnung zusammenfasst, ist derzeit nicht festgelegt.

Ebenso ist derzeit keine `latest`-Tag-Strategie festgelegt. Ein `latest`-Tag darf deshalb nicht ohne eine zuvor ausdrücklich festgelegte Veröffentlichungsregel verwendet oder als vorhanden vorausgesetzt werden.

Bis entsprechende Entscheidungen getroffen und dokumentiert sind, müssen architekturspezifische Artefakte eindeutig ihrer Version und Zielarchitektur zugeordnet werden können.

## Verpflichtende Veröffentlichungsbestandteile

Zu einer öffentlichen Veröffentlichung von OrdnerBrowse gehören mindestens:

- der eindeutig versionierte und freigegebene Quellstand,
- die zum veröffentlichten Stand gehörende öffentliche Projektdokumentation,
- `LICENSE`,
- `THIRD_PARTY_NOTICES.md`,
- `THIRD_PARTY_NOTICES.en.md`,
- die mitgeführten Drittanbieter-Lizenztexte unter `LICENSES/`,
- ein zum veröffentlichten Stand konsistenter öffentlicher Changelog,
- die erforderlichen Nachweise, dass die für diesen Stand vorgesehenen Build-, Test- und Freigabe-Gates erfolgreich abgeschlossen wurden.

Alle Veröffentlichungsbestandteile müssen demselben freigegebenen Projektstand zugeordnet werden können und hinsichtlich Versionsangaben, Lizenzinformationen und Dokumentation konsistent sein.

Interne Prüfprotokolle müssen dafür nicht zwangsläufig selbst öffentlich veröffentlicht werden. Entscheidend ist, dass die vorgeschriebenen Prüfungen revisionssicher durchgeführt und der erfolgreiche Abschluss des jeweiligen Freigabe-Gates nachgewiesen wurde.

Falls Containerartefakte öffentlich bereitgestellt werden, müssen auch diese eindeutig dem veröffentlichten Projektstand und der jeweiligen Zielarchitektur zugeordnet werden können.

## Release-Gates

Vor einer formalen Veröffentlichung müssen alle für den jeweiligen Stand vorgesehenen Release-Gates erfolgreich abgeschlossen und nachvollziehbar dokumentiert sein.

Dazu gehören mindestens:

- der freizugebende Quellstand ist eindeutig bestimmt und seiner Versionsnummer zugeordnet,
- die vorgesehenen Builds wurden erfolgreich abgeschlossen,
- die vorgesehenen automatisierten Regressionstests wurden erfolgreich abgeschlossen,
- vorgesehene Containerartefakte wurden für ihre jeweilige Zielarchitektur erfolgreich gebaut und geprüft,
- die Veröffentlichungsbestandteile sind vollständig und demselben Projektstand zugeordnet,
- Versionsangaben, öffentliche Dokumentation, Changelog und Lizenzinformationen sind untereinander konsistent,
- der zur Veröffentlichung vorgesehene Quellstand und die Veröffentlichungsartefakte enthalten keine produktiven Secrets, persönlichen Zugangsdaten, produktiven Laufzeitdateien oder anderen vertraulichen Inhalte.

Ein technisch erfolgreicher Build oder Testlauf stellt für sich allein noch keine Freigabe dar.

Die formale Veröffentlichung erfolgt erst nach erfolgreichem Abschluss aller vorgesehenen Prüfungen und einer ausdrücklich dokumentierten Freigabe.

Wird eines der vorgesehenen Release-Gates nicht bestanden oder fehlt ein erforderlicher Nachweis, darf der betreffende Stand nicht als formal veröffentlicht bezeichnet werden.

## Interne Nachweise

Interne Prüf- und Freigabenachweise dienen der revisionssicheren Absicherung des Entwicklungs- und Veröffentlichungsprozesses.

Dazu können insbesondere gehören:

- Build- und Testprotokolle,
- Prüfsummen und Dateilisten,
- Nachweise zu Containerbuilds und Zielarchitekturen,
- interne Freigabe- und Rückfallnachweise,
- lokale Entwicklungs- und Referenzpfade,
- systemspezifische Betriebs- und Arbeitsverzeichnisse.

Diese internen Nachweise müssen nicht Bestandteil der öffentlichen Veröffentlichung sein.

In der öffentlichen Projektdokumentation sollen keine lokalen Benutzerpfade, systemspezifischen internen Arbeitsverzeichnisse, produktiven Laufzeitpfade, vertraulichen Betriebsinformationen oder internen Freigabeprotokolle veröffentlicht werden, sofern sie für die Nutzung des Projekts nicht erforderlich sind.

Öffentlich dokumentiert werden stattdessen die für Anwender relevanten Voraussetzungen, Konfigurationsregeln, Lizenzinformationen, Versionsangaben und die jeweils vorgesehene Form der Veröffentlichung.

Interne Nachweise bleiben davon unberührt und werden nach den für das Projekt festgelegten Prüf- und Freigaberegeln geführt.
