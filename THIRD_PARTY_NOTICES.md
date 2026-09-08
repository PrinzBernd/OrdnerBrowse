# Hinweise zu Drittanbieter-Komponenten

OrdnerBrowse verwendet und verteilt Komponenten Dritter. Die nachstehende
Übersicht dient der Zuordnung. Maßgeblich sind jeweils die vollständigen
Lizenztexte unter `LICENSES/` sowie bereits in den Fremdkomponenten enthaltene
Copyright- und Lizenzhinweise.

## Web-Bibliotheken

### Bootstrap 5.3.3
- Lizenz: MIT
- Im Repository als vendorte Web-Bibliothek enthalten.
- Lizenztext: `LICENSES/Bootstrap-5.3.3-MIT.txt`

### Popper / @popperjs/core 2.11.8
- Lizenz: MIT
- In den Bootstrap-Bundle-Dateien enthalten bzw. von Bootstrap verwendet.
- Lizenztext: `LICENSES/Popper-2.11.8-MIT.txt`

## PDF.js

### PDF.js / pdfjs-dist 6.2.108
- Lizenz: Apache-2.0
- Lizenztext: `LICENSES/PDFjs-6.2.108-Apache-2.0.txt`

Der mitgelieferte PDF.js-Bestand enthält zusätzliche Drittanbieterbestandteile
mit eigenen Lizenzbedingungen. Die vorhandenen Originaltexte werden zentral
unverändert mitgeführt:

- Adobe CMaps: `LICENSES/PDFjs-CMaps-Adobe.txt`
- Foxit/PDFium-Schriften: `LICENSES/PDFjs-Foxit-PDFium.txt`
- Liberation Fonts / SIL Open Font License 1.1:
  `LICENSES/PDFjs-Liberation-SIL-OFL-1.1.txt`
- JBIG2/PDFium und zugehörige PDF.js-Anteile:
  `LICENSES/PDFjs-JBIG2.txt` und `LICENSES/PDFjs-PDFJS-JBIG2.txt`
- OpenJPEG und zugehörige PDF.js-Anteile:
  `LICENSES/PDFjs-OpenJPEG.txt` und `LICENSES/PDFjs-PDFJS-OpenJPEG.txt`
- QCMS und zugehörige PDF.js-Anteile:
  `LICENSES/PDFjs-QCMS-MIT.txt` und `LICENSES/PDFjs-PDFJS-QCMS.txt`
- QuickJS: MIT, Lizenztext `LICENSES/QuickJS-MIT.txt`

## .NET-/OIDC-Abhängigkeiten

Direkte Abhängigkeit:

- Microsoft.AspNetCore.Authentication.OpenIdConnect 10.0.10 — MIT

Zusätzlich werden über die direkte OIDC-Abhängigkeit folgende transitive
Microsoft-Komponenten verwendet:

- Microsoft.IdentityModel.Abstractions
- Microsoft.IdentityModel.JsonWebTokens
- Microsoft.IdentityModel.Logging
- Microsoft.IdentityModel.Protocols
- Microsoft.IdentityModel.Protocols.OpenIdConnect
- Microsoft.IdentityModel.Tokens
- System.IdentityModel.Tokens.Jwt

Die für eine Veröffentlichung tatsächlich aufgelösten Paketversionen werden
im Rahmen der Release-Prüfung verifiziert.

Diese Microsoft-Komponenten stehen unter der MIT-Lizenz. Der zugehörige
Lizenztext wird unter `LICENSES/Microsoft-Identity-MIT.txt` mitgeführt.

## Projektlizenz

OrdnerBrowse selbst wird unter `AGPL-3.0-or-later` veröffentlicht.
Der vollständige Lizenztext befindet sich in `LICENSE`.
