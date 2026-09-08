# Performance-Diagnostik

## 1. Ziel

Die Performance-Diagnostik protokolliert ausschließlich technische Laufzeit- und Ablaufzeiten der WebUI. Sie dient dazu, langsame oder auffällige Abläufe in Navigation, Cache, Dokumentlisten, Detailabrufen und Rendering einordnen zu können, ohne produktive Dokumentinhalte zu protokollieren.

Die Funktion verändert keine Paperless-Daten und bleibt innerhalb der fachlich read-only arbeitenden WebUI.

## 2. Messläufe und Logformat

Zusammengehörige Messungen können über eine zufällig erzeugte Messlaufkennung zusammengeführt werden:

```text
PERF-<Zeitstempel>-<Zufallsanteil>
```

Die Tagesdatei enthält tabulatorgetrennt folgende Spalten:

```text
ZeitUtc	Version	Messlauf	Klasse	Operation	Ergebnis	DauerMs	Details
```

Jeder relevante Eintrag enthält damit insbesondere:

- UTC-Zeitpunkt;
- vollständige WebUI-Version;
- Messlaufkennung;
- technische Datenklasse bzw. Ablaufgruppe;
- technische Operation;
- Ergebnisstatus;
- gemessene Dauer in Millisekunden;
- optional neutrale technische Zusatzangaben.

## 3. Erfasste technische Bereiche

Die aktuelle Implementierung verwendet Performance-Diagnostik unter anderem für:

- Initialisierung der Hauptansicht;
- Rendering-Schritte;
- Navigationsaufbau und inkrementelle Navigation;
- Cache-Laden und Cache-Speichern;
- Dokumentlisten und Seitenabrufe;
- Dokumentdetails und Thumbnail-Abrufe;
- technische Synchronisationsabläufe.

Die Liste beschreibt den aktuellen technischen Einsatzbereich und ist keine fachliche Änderung der Dokumentverarbeitung.

## 4. Fehlerdiagnose

Bei technischen Fehlern werden nur neutrale Angaben protokolliert, beispielsweise:

- ob ein Timeout in Betracht kommt;
- Ausnahmetyp;
- HTTP-Statuscode, soweit vorhanden;
- neutrale technische Zähler oder Ablaufangaben.

Der Diagnosepfad protokolliert keine Exception-Nachricht, wenn dadurch sensible Inhalte unkontrolliert übernommen werden könnten.

## 5. Verbotene Inhalte

Nicht in Performance-Diagnoseprotokolle gehören insbesondere:

- Kennwörter;
- API-Tokens oder persönliche Paperless-Tokens;
- Cookies;
- OIDC-Autorisierungscodes;
- PKCE-Verifier oder PKCE-Challenges;
- Client Secrets;
- private Schlüssel oder Zertifikatsinhalte;
- produktive Dokumentinhalte;
- echte Personen-, Mandanten-, Korrespondenten- oder Dokumentnamen.

Zusatzangaben müssen deshalb auf technische, nicht sensible Informationen begrenzt bleiben.

## 6. Konfiguration

Die Performance-Diagnostik wird über folgende Konfigurationswerte gesteuert:

```text
WebUi:PerformanceDiagnostics:LogDirectory
WebUi:PerformanceDiagnostics:RetentionDays
```

`RetentionDays` muss zwischen `1` und `365` liegen. Ein ausdrücklich konfiguriertes `LogDirectory` muss ein absoluter Pfad sein.

Für die vorgesehenen Containerprofile gilt:

```text
LogDirectory=/data/performance-diagnostics
RetentionDays=7
```

Wenn kein Logverzeichnis konfiguriert ist, verwendet die lokale Anwendung einen temporären Unterordner unter:

```text
WebUI/performance-diagnostics
```

## 7. Tagesdateien und Aufbewahrung

Die Ablage erfolgt tageweise mit dem Dateimuster:

```text
performance-diagnostics-YYYY-MM-DD.log
```

Die Bereinigung ist ausschließlich auf Dateien mit dem eigenen Präfix

```text
performance-diagnostics-
```

im konfigurierten Diagnoseverzeichnis begrenzt.

Dateien, deren letzte Änderung älter als die konfigurierte Aufbewahrungsdauer ist, werden regelmäßig entfernt. Die Bereinigung wird höchstens ungefähr einmal pro Stunde angestoßen.

Fehler beim Schreiben oder Bereinigen der Performance-Diagnose werden intern abgefangen und dürfen den fachlichen WebUI-Ablauf nicht abbrechen.

## 8. Containerbetrieb

Im Container wird ein eigenes Performance-Diagnoseverzeichnis schreibbar eingebunden:

```text
/data/performance-diagnostics
```

Der Hostbereich liegt unter:

```text
<Runtime-Root>/diagnostic/performance/
```

Für diesen Bereich gelten insbesondere:

- RW-Mount;
- Eigentümer entsprechend `APP_UID:APP_GID`;
- Verzeichnismodus `700` unter Linux;
- Aufbewahrung `7` Tage.

## 9. Abgrenzung zu OIDC-Diagnostik und Docker-Logs

Performance-Diagnostik, OIDC-Diagnostik und normale Container-Konsolenlogs sind getrennte Diagnosekanäle.

Die Performance-Diagnostik misst technische Ablaufzeiten. Die OIDC-Diagnostik dient der neutralen Analyse des Authentifizierungsablaufs. Docker-Konsolenlogs bleiben über den konfigurierten Logging-Treiber und dessen Größenrotation verwaltet.

## 10. Statusgrenze

Die Performance-Diagnostik ist eine technische Beobachtungsfunktion. Sie erzeugt keine Dokumentvorschauen, verändert keine Paperless-Daten und führt keine schreibenden Paperless-API-Methoden ein.

Die WebUI bleibt fachlich read-only gegenüber Paperless-ngx.
