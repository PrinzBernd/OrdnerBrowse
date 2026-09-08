# OIDC-Diagnostik

## 1. Ziel

Die OIDC-Fehler-, PKCE- und Transaktionsdiagnostik wird als einheitliche technische Diagnose geführt.

Sie dient dazu, sporadische OIDC-Fehler auch dann noch belastbar einordnen zu können, wenn sie sich nicht gezielt reproduzieren lassen.

## 2. Einheitliche Logger-Kategorie

Alle sicheren OIDC-Diagnoseereignisse verwenden:

```text
WebUI.OidcDiagnostics
```

Dazu gehören insbesondere:

- Challenge;
- Callback;
- TokenRequest;
- TokenResponse;
- RemoteFailure;
- neutrale Klassifikation des OIDC-Rücksprungfehlers.

## 3. Zulässige Diagnoseinformationen

Zulässig sind ausschließlich neutrale technische Angaben, beispielsweise:

- zufällige Transaktionskennung `TX-XXXXXXXX`;
- Ursprung der Challenge;
- JA/NEIN/NICHT_PRUEFBAR-/NICHT_BEKANNT-Klassifikationen;
- Vorhandensein von Verifier, Challenge, Authorization Code oder `state`;
- interne PKCE-Konsistenz;
- Vergleichsergebnis State-Verifier zu TokenRequest;
- Vergleichsergebnis Callback-Code zu TokenRequest;
- Erreichen oder Nichterreichen der Tokenantwort;
- neutrale Fehlerkategorie;
- Ausnahmetyp;
- Anzahl von Korrelations- und Nonce-Cookies;
- technische HTTPS-/Forwarded-Headers-/Antwortstatusangaben.

## 4. Verbotene Inhalte

Nicht protokolliert werden dürfen insbesondere:

- Authorization Codes;
- `state`-Inhalte;
- PKCE-Verifier oder Code Challenges;
- ID-, Access- oder Refresh-Tokens;
- Cookie-Inhalte;
- Client Secrets;
- private Schlüssel oder Zertifikatsinhalte;
- Paperless-Benutzertokens;
- echte Personen-, Mandanten-, Korrespondenten- oder Dokumentnamen.

Die `TX-...`-Kennung wird zufällig erzeugt und nicht aus Benutzer-, Code-, State-, PKCE-, Cookie- oder Tokenwerten abgeleitet.

## 5. Transaktionskette

Die `TX-...`-Kennung wird in den geschützten OIDC-AuthenticationProperties mitgeführt und ist für die Kette vorgesehen:

```text
Challenge
→ Callback
→ TokenRequest
→ TokenResponse oder RemoteFailure
```

Beim neutralen OIDC-Rücksprungfehler wird dieselbe Transaktionskennung ebenfalls ausgegeben, soweit sie im konkreten Fehlerpfad wiederhergestellt werden konnte.

## 6. Dateiprotokollierung und Aufbewahrung

Die Anwendung kann zusätzlich zur normalen Konsolenausgabe ausschließlich die OIDC-Diagnosekategorie in ein eigenes Verzeichnis schreiben.

Aktivierung:

```text
WebUi:OidcDiagnostics:LogDirectory
WebUi:OidcDiagnostics:RetentionDays
```

Für den Containerbetrieb gilt:

```text
LogDirectory=/data/oidc-diagnostics
RetentionDays=7
```

Jeder OIDC-Diagnoseeintrag enthält zusätzlich die vollständige WebUI-Version. Die Dateiablage erfolgt tageweise mit dem Präfix:

```text
oidc-diagnostics-
```

Eigene Diagnoseprotokolle, deren letzte Änderung mehr als die konfigurierte Aufbewahrungsdauer zurückliegt, werden beim Start und während des Betriebs regelmäßig entfernt.

Löschungen sind strikt auf Dateien mit dem eigenen Präfix im konfigurierten Diagnoseverzeichnis begrenzt.

Ein Fehler beim Schreiben oder Bereinigen der Diagnoseprotokolle darf den OIDC-Authentifizierungsablauf nicht verändern oder abbrechen.

## 7. Docker-Konsolenlogs

Die normale Container-Konsolenausgabe bleibt getrennt.
Die neutralen Compose-Vorlagen verwenden weiterhin den Docker-Logging-Treiber `json-file` mit Größenrotation.

Die zeitbasierte Sieben-Tage-Aufbewahrung gilt für OIDC- und Performance-Diagnose; die Docker-Konsolenlogs bleiben über Größe und Dateianzahl begrenzt.

## 8. Temporäre allgemeine Debug-Logs

Zusätzlich aktivierte allgemeine Kategorien wie ASP.NET-Core-Hosting, Routing, Connections oder SignalR auf erhöhten LogLeveln sind Diagnosehilfen für konkrete Testphasen.

Sie gehören nicht zum regulären Grundbetrieb und werden nur nach ausdrücklicher Entscheidung für einen konkreten Testlauf aktiviert.

## 9. Statusgrenze

Die dauerhafte OIDC-Diagnostik ist eine technische Beobachtungsfunktion und keine Änderung der fachlichen Paperless-Funktionalität.

Die WebUI bleibt read-only gegenüber Paperless-ngx.
