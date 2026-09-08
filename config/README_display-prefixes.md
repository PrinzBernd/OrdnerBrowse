# Display-Präfixe in OrdnerBrowse

## 1. Zweck

OrdnerBrowse kann definierte Präfixe bei der **Anzeige** von Bezeichnungen ausblenden.

Die zugrunde liegenden Werte in paperless-ngx werden dabei **nicht verändert**. Die Funktion wirkt ausschließlich auf die Darstellung in OrdnerBrowse und bleibt damit innerhalb des fachlichen Read-only-Prinzips.

Präfixregeln können getrennt für vier Bereiche festgelegt werden:

- Bereiche beziehungsweise Speicherpfade (`Areas`)
- Korrespondenten (`Correspondents`)
- Dokumenttypen (`DocumentTypes`)
- Dokumenttitel (`Documents`)

## 2. Konfigurationsdatei

Die neutrale Vorlage im Quellstand befindet sich unter:

```text
config/display-prefixes.example.json
```

Sie enthält absichtlich keine aktiven Beispielregeln:

```json
{
  "Areas": [],
  "Correspondents": [],
  "DocumentTypes": [],
  "Documents": []
}
```

Für den Containerbetrieb wird die optionale Laufzeitdatei hostseitig unter folgendem relativen Pfad des Runtime-Roots abgelegt:

```text
config/display-prefixes.json
```

Der Container liest sie read-only über:

```text
/data/config/display-prefixes.json
```

Im lokalen macOS-Betrieb verwendet OrdnerBrowse ohne ausdrücklich konfigurierten anderen Pfad standardmäßig:

```text
~/Library/Application Support/webui/config/display-prefixes.json
```

Die Datei ist optional. Fehlt sie oder ist sie leer, werden Bezeichnungen unverändert angezeigt.

## 3. Grundprinzip

Eine Regel beschreibt ausschließlich den Anfang einer Bezeichnung, der in OrdnerBrowse nicht angezeigt werden soll.

Beispiel:

```json
{
  "Documents": ["D_"]
}
```

Aus:

```text
D_Rechnung
```

wird in OrdnerBrowse:

```text
Rechnung
```

Die ursprüngliche Bezeichnung `D_Rechnung` in paperless-ngx bleibt unverändert.

## 4. Regeln für die vier Bereiche

Beispielkonfiguration:

```json
{
  "Areas": ["A_"],
  "Correspondents": ["K_"],
  "DocumentTypes": ["T_"],
  "Documents": ["D_"]
}
```

Beispielwirkung:

| Bereich | Originalbezeichnung | Anzeige in OrdnerBrowse |
|---|---|---|
| `Areas` | `A_Buchhaltung` | `Buchhaltung` |
| `Correspondents` | `K_Beispiel` | `Beispiel` |
| `DocumentTypes` | `T_Rechnung` | `Rechnung` |
| `Documents` | `D_Monatsabrechnung` | `Monatsabrechnung` |

Die Regellisten wirken getrennt voneinander. Eine Regel unter `Documents` wird beispielsweise nicht automatisch auf Korrespondenten angewendet.

## 5. Unterstützte Musterarten

Eine Präfixregel kann aus festem Text, definierten Datumsplatzhaltern und frei kombinierbaren Zeichenmustern bestehen.

Die Sonderbedeutung von Datumsplatzhaltern sowie `?` und `#` gilt ausschließlich innerhalb von geschweiften Klammern `{...}`. Außerhalb geschweifter Klammern sind Zeichen Bestandteil des festen Textes einer Regel.

### 5.1 Datumsplatzhalter

OrdnerBrowse unterstützt genau folgende Datumsplatzhalter:

| Platzhalter | Beispiel |
|---|---|
| `{yyyyMMdd}` | `20260228` |
| `{yyMMdd}` | `260228` |
| `{yyyy-MM-dd}` | `2026-02-28` |
| `{yy-MM-dd}` | `26-02-28` |
| `{yyyy.MM.dd}` | `2026.02.28` |
| `{yy.MM.dd}` | `26.02.28` |
| `{ddMMyyyy}` | `28022026` |
| `{ddMMyy}` | `280226` |
| `{dd-MM-yyyy}` | `28-02-2026` |
| `{dd-MM-yy}` | `28-02-26` |
| `{dd.MM.yyyy}` | `28.02.2026` |
| `{dd.MM.yy}` | `28.02.26` |

Dabei gilt:

```text
yyyy = vierstellige Jahreszahl
yy   = zweistellige Jahreszahl
MM   = zweistelliger Monat
dd   = zweistelliger Tag
```

Die Platzhalter müssen exakt in der dokumentierten Groß-/Kleinschreibung verwendet werden. Insbesondere sind `yyyy` und `yy` klein geschrieben. Varianten wie `{YYYYMMdd}` oder `{YYMMdd}` sind keine gültigen Datumsplatzhalter.

Der Datumsanteil muss ein tatsächlich gültiges Kalenderdatum bilden. Beispielsweise ist `28-02-2026` gültig, `30-02-2026` dagegen nicht.

Bei zweistelligen Jahreszahlen wird `yy` für die Kalenderprüfung deterministisch dem Bereich `2000` bis `2099` zugeordnet. `00` wird dabei als `2000`, `26` als `2026` und `99` als `2099` geprüft. Diese Zuordnung dient ausschließlich der Gültigkeitsprüfung des Präfixes; OrdnerBrowse verändert oder speichert daraus kein Datum.

Beispielregel:

```json
{
  "Documents": ["Archiv_{dd-MM-yyyy}_"]
}
```

Aus:

```text
Archiv_28-02-2026_Rechnung
```

wird:

```text
Rechnung
```

### 5.2 Zeichenmuster mit `?` und `#`

Innerhalb eines Zeichenmusters haben `?` und `#` folgende Bedeutung:

| Symbol | Bedeutung |
|---|---|
| `?` | genau **ein Unicode-Buchstabe** oder genau eine ASCII-Ziffer `0` bis `9` |
| `#` | genau **eine ASCII-Ziffer `0` bis `9`** |

`?` akzeptiert damit beispielsweise auch `ä`, `ö`, `ü`, `Ä`, `Ö`, `Ü`, `ß` und andere Unicode-Buchstaben. Satzzeichen, Trennzeichen, Leerzeichen und andere Symbole werden von `?` nicht akzeptiert.

`#` akzeptiert ausschließlich die ASCII-Ziffern `0` bis `9`. Andere Unicode-Ziffern werden nicht als `#` gewertet.

Alle anderen Zeichen innerhalb eines Zeichenmusters sind feste Zeichen und müssen an derselben Stelle vorkommen.

Beispiele:

| Regel | Passender Anfang | Wirkung |
|---|---|---|
| `{??????}_` | `AB12ä9_` | sechs Buchstaben/Ziffern und `_` werden entfernt |
| `{######}_` | `240906_` | sechs Ziffern und `_` werden entfernt |
| `{??-##}_` | `Ä7-42_` | zwei Buchstaben/Ziffern, `-`, zwei Ziffern und `_` werden entfernt |
| `{##.??}_` | `24.ö7_` | zwei Ziffern, `.`, zwei Buchstaben/Ziffern und `_` werden entfernt |

Nicht passend sind beispielsweise:

```text
Regel {??????}_  -> AB-2ä9_Dokument
Regel {######}_  -> 24A906_Dokument
Regel {??-##}_   -> A_-12_Dokument
```

Ein festes Zeichen außerhalb des Musters ist **nicht automatisch erforderlich** und wird auch nicht automatisch entfernt.

Beispiel:

```text
Regel {??}    auf AB_Dokument  -> _Dokument
Regel {??}_   auf AB_Dokument  -> Dokument
```

`{??}` entfernt damit ausschließlich die ersten zwei passenden Zeichen. Der nachfolgende Unterstrich bleibt erhalten, weil er nicht Bestandteil der Regel ist. Erst `{??}_` verlangt zusätzlich einen Unterstrich an dieser Stelle und entfernt ihn zusammen mit den beiden passenden Zeichen.

Dasselbe Prinzip gilt für `#` und für andere feste Zeichen:

```text
{##}     -> zwei ASCII-Ziffern
{##}_    -> zwei ASCII-Ziffern plus `_`
{##-??}  -> zwei ASCII-Ziffern, `-`, danach zwei `?`-Zeichen
```

Ein Klammerausdruck, der weder ein unterstützter Datumsplatzhalter ist noch mindestens ein `?` oder `#` enthält, ist ungültig. Beispielsweise ist `{ABC}` kein Zeichenmuster. Fester Text wird ohne geschweifte Klammern geschrieben.

## 6. Feste Texte und Muster kombinieren

Fester Text, Datumsplatzhalter und Zeichenmuster können innerhalb einer Regel frei kombiniert werden.

Beispiele:

```json
{
  "Documents": [
    "Scan_{yyyy-MM-dd}_",
    "{??-##}_",
    "Archiv_{dd.MM.yyyy}_{??}_"
  ]
}
```

Aus:

```text
Scan_2026-09-05_Vertrag
```

wird:

```text
Vertrag
```

Aus:

```text
Ä7-42_Dokument
```

wird:

```text
Dokument
```

Aus:

```text
Archiv_05.09.2026_A7_Rechnung
```

wird:

```text
Rechnung
```

Mehrere Mustersegmente in derselben Regel sind zulässig. Jedes Segment muss an der vorgesehenen Stelle vollständig passen.

## 7. Regeln wirken nur am Anfang

Eine Präfixregel wird ausschließlich angewendet, wenn sie direkt am Anfang der Bezeichnung passt.

Bei der Regel:

```json
{
  "Documents": ["Archiv_{yyyy-MM-dd}_"]
}
```

wird:

```text
Archiv_2026-02-28_Rechnung
```

gekürzt, aber:

```text
X_Archiv_2026-02-28_Rechnung
```

bleibt unverändert.

## 8. Trennzeichen sind Teil der Regel

OrdnerBrowse entfernt keine zusätzlichen Trennzeichen automatisch.

Beispiel:

```json
{
  "Documents": ["{yyyyMMdd}"]
}
```

Aus:

```text
20260903_Rechnung
```

wird:

```text
_Rechnung
```

Soll auch der Unterstrich verschwinden, muss er Bestandteil der Regel sein:

```json
{
  "Documents": ["{yyyyMMdd}_"]
}
```

Dann wird daraus:

```text
Rechnung
```

Dasselbe gilt für Zeichenmuster. Sowohl:

```text
{??-##}_
```

als auch ein entsprechend innerhalb des Musters fest definiertes Trennzeichen müssen vollständig in der Regel enthalten sein, wenn sie entfernt werden sollen.

## 9. Mehrere passende Regeln

Sind mehrere Regeln für dieselbe Bezeichnung passend, wird der **längste tatsächlich passende Präfix** entfernt.

Beispiel:

```json
{
  "Documents": ["A_", "A_B_"]
}
```

Aus:

```text
A_B_Name
```

wird:

```text
Name
```

Pro Bezeichnung wird höchstens **eine** Präfixregel angewendet. Nach dem Entfernen eines passenden Präfixes wird das Ergebnis nicht erneut mit der Regelliste bearbeitet.

## 10. Groß- und Kleinschreibung

Feste Textbestandteile einer Präfixregel werden ohne Unterscheidung von Groß- und Kleinschreibung verglichen. Das gilt auch für feste Buchstaben innerhalb eines `?`-/`#`-Zeichenmusters.

Eine feste Regel wie:

```text
D_
```

kann daher auch auf einen entsprechend geschriebenen Anfang wie `d_` passen.

Datumsplatzhalter und die Mustersymbole selbst müssen dagegen exakt in der dokumentierten Schreibweise verwendet werden. `{yyyyMMdd}` ist gültig; `{YYYYMMdd}` ist ungültig.

## 11. Ungültige Einzelregeln

Ungültige Einzelregeln werden ignoriert. Andere gültige Regeln derselben Konfigurationsdatei bleiben verwendbar.

Beispiele für ungültige Regeln sind insbesondere:

- leere Regelwerte;
- unbekannte Datumsplatzhalter wie `{yyyyXYZ}`;
- falsch geschriebene Datumsplatzhalter wie `{YYYYMMdd}`;
- Klammerausdrücke ohne unterstützten Datumsplatzhalter und ohne `?` oder `#`, beispielsweise `{ABC}`;
- Werte, die keine Zeichenkette sind;
- leere oder fehlerhaft aufgebaute Platzhalterklammern.

Unbekannte JSON-Bereiche erzeugen keine zusätzliche Display-Präfix-Funktion.

## 12. Verhalten bei fehlender oder fehlerhafter Datei

Die Display-Präfix-Datei ist optional.

- Fehlt sie beim Start, bleiben die Bezeichnungen unverändert.
- Ist sie leer, bleiben die Bezeichnungen unverändert.
- Ist sie beim Start syntaktisch ungültig, wird sie ignoriert und die Anzeige bleibt unverändert.
- Wird eine bereits gültig geladene Datei später syntaktisch ungültig, bleibt die **zuletzt gültige Konfiguration** aktiv.
- Eine spätere gültige Änderung wird automatisch neu geladen.

## 13. Vollständiges Beispiel

Das folgende Beispiel dient ausschließlich der Erläuterung. Es ist nicht als notwendige Standardkonfiguration zu verstehen.

```json
{
  "Areas": [
    "A_"
  ],
  "Correspondents": [
    "K_"
  ],
  "DocumentTypes": [
    "T_"
  ],
  "Documents": [
    "D_",
    "Archiv_{dd-MM-yyyy}_",
    "{??-##}_"
  ]
}
```

Mögliche Wirkung:

| Originalbezeichnung | Regelbereich | Anzeige in OrdnerBrowse |
|---|---|---|
| `A_Buchhaltung` | `Areas` | `Buchhaltung` |
| `K_Beispiel` | `Correspondents` | `Beispiel` |
| `T_Rechnung` | `DocumentTypes` | `Rechnung` |
| `D_Monatsabrechnung` | `Documents` | `Monatsabrechnung` |
| `Archiv_28-02-2026_Rechnung` | `Documents` | `Rechnung` |
| `Ä7-42_Dokument` | `Documents` | `Dokument` |

## 14. Read-only-Grenze

Display-Präfixe verändern ausschließlich die sichtbare Darstellung in OrdnerBrowse.

Sie ändern insbesondere nicht:

- Speicherpfade beziehungsweise Bereiche in paperless-ngx;
- Korrespondenten in paperless-ngx;
- Dokumenttypen in paperless-ngx;
- Dokumenttitel in paperless-ngx;
- Dokumente oder Metadaten in paperless-ngx.

Die Funktion führt keine schreibende Paperless-API-Operation aus.
