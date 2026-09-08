# Third-Party Notices

OrdnerBrowse uses and distributes third-party components. The overview below
maps those components to their licences. The complete licence texts under
`LICENSES/`, together with copyright and licence notices already contained in
the third-party components, are authoritative.

## Web libraries

### Bootstrap 5.3.3
- Licence: MIT
- Vendored in the repository as a web library.
- Licence text: `LICENSES/Bootstrap-5.3.3-MIT.txt`

### Popper / @popperjs/core 2.11.8
- Licence: MIT
- Included in Bootstrap bundle files or used by Bootstrap.
- Licence text: `LICENSES/Popper-2.11.8-MIT.txt`

## PDF.js

### PDF.js / pdfjs-dist 6.2.108
- Licence: Apache-2.0
- Licence text: `LICENSES/PDFjs-6.2.108-Apache-2.0.txt`

The bundled PDF.js distribution contains additional third-party components
with their own licence terms. Their existing original licence texts are
preserved centrally:

- Adobe CMaps: `LICENSES/PDFjs-CMaps-Adobe.txt`
- Foxit/PDFium fonts: `LICENSES/PDFjs-Foxit-PDFium.txt`
- Liberation Fonts / SIL Open Font License 1.1:
  `LICENSES/PDFjs-Liberation-SIL-OFL-1.1.txt`
- JBIG2/PDFium and related PDF.js portions:
  `LICENSES/PDFjs-JBIG2.txt` and `LICENSES/PDFjs-PDFJS-JBIG2.txt`
- OpenJPEG and related PDF.js portions:
  `LICENSES/PDFjs-OpenJPEG.txt` and `LICENSES/PDFjs-PDFJS-OpenJPEG.txt`
- QCMS and related PDF.js portions:
  `LICENSES/PDFjs-QCMS-MIT.txt` and `LICENSES/PDFjs-PDFJS-QCMS.txt`
- QuickJS: MIT, licence text `LICENSES/QuickJS-MIT.txt`

## .NET / OIDC dependencies

Direct dependency:

- Microsoft.AspNetCore.Authentication.OpenIdConnect 10.0.10 — MIT

The following transitive Microsoft components are also used through the direct
OIDC dependency:

- Microsoft.IdentityModel.Abstractions
- Microsoft.IdentityModel.JsonWebTokens
- Microsoft.IdentityModel.Logging
- Microsoft.IdentityModel.Protocols
- Microsoft.IdentityModel.Protocols.OpenIdConnect
- Microsoft.IdentityModel.Tokens
- System.IdentityModel.Tokens.Jwt

The package versions actually resolved for a publication are verified as part
of the release checks.

These Microsoft components are licensed under MIT. The corresponding licence
text is included as `LICENSES/Microsoft-Identity-MIT.txt`.

## Project licence

OrdnerBrowse itself is distributed under `AGPL-3.0-or-later`.
The complete licence text is provided in `LICENSE`.
