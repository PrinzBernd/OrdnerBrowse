# Changelog

This changelog describes the publicly released versions of OrdnerBrowse and
their publicly relevant changes.

Internal development states, local validation states, and versions preceding
the first public release are not listed here as releases.

## v09.91.1 – First public beta release

### Features

- Independent, unofficial, document-oriented web UI for paperless-ngx with
  read-only access only.
- Multi-level navigation through areas or storage paths, correspondents,
  document types, and documents.
- Filtering of correspondents and document types within the selected navigation
  scope.
- Search for terms in document titles with support for AND (`&`) and OR (`|`)
  across multiple search terms.
- Sorting of areas, correspondents, document types, and documents.
- Document preview using the thumbnail provided by paperless-ngx with a
  vertically scrollable detail area.
- Multi-page PDF-Quick Look directly in OrdnerBrowse.
- Display of important document information such as tags, custom fields,
  metadata, and document notes stored in paperless-ngx, including user
  information and date, in both the document preview and PDF-Quick Look.
- Direct switch to the selected document in the paperless-ngx frontend.
- Customisable interface with resizable or collapsible columns and a movable,
  resizable PDF-Quick Look.
- Externally configurable display prefixes for areas or storage paths,
  correspondents, document types, and document titles.
- Support for fixed display prefixes, defined date formats, and freely combinable `?`/`#` character patterns.
- Valid changes to the display-prefix configuration can be reloaded at runtime
  without changing the underlying labels in paperless-ngx.

### Operation and configuration

- Container operation for ARM64 and AMD64 prepared on a common source-code
  basis.
- Dockerfile, Compose template, and helpers for configuration, validation, and
  controlled container start provided.
- Runtime configuration, secrets, and persistent data kept separate from public
  source code.
- Public configuration examples use neutral placeholders only.
- Display prefixes are provided through an external JSON configuration.
- OpenID Connect (OIDC) is used for sign-in during regular operation.
- Local development and test operation is separated from regular OIDC sign-in.
- Health check as well as OIDC and performance diagnostics are documented for
  container operation.
- Container images are not loaded automatically from a registry; the documented
  operation uses `pull_policy: never`.

### Security and privacy

- OrdnerBrowse is functionally read-only towards paperless-ngx.
- paperless-ngx remains the sole source of data, documents, users, and
  permissions.
- Writing Paperless API methods for documents, metadata, users, or permissions
  are not part of the feature set.
- Personal Paperless connections and security-relevant runtime data are handled
  server-side or outside the public source code.
- Passwords, API tokens, 2FA codes, client secrets, private keys, original
  documents, and productive runtime files do not belong in the public source
  tree or public project artefacts.
- Security, support, and contribution documentation describes the intended
  boundaries for secrets, real data, and productive information.

### Documentation and licensing

- OrdnerBrowse is distributed under `AGPL-3.0-or-later`.
- Third-party components and their licence terms are documented in
  `THIRD_PARTY_NOTICES.en.md`.
- Required third-party licence texts are bundled under `LICENSES/`.
- Public documentation for architecture, configuration, container operation,
  security, support, and contributions is part of the project.
- Documentation for the external display-prefix configuration, including a
  neutral example configuration, is included.
