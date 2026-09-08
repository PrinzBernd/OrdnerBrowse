# OrdnerBrowse – a Web UI for paperless-ngx

OrdnerBrowse is an independent web interface for clearly browsing and viewing documents from paperless-ngx.

It allows users to browse and search areas or storage paths, correspondents, document types, and documents including their metadata. Documents can be searched, sorted, previewed, and displayed together with their related information. A larger multi-page PDF-Quick Look is also available for viewing documents.

OrdnerBrowse is functionally read-only towards paperless-ngx. Documents, metadata, users, and permissions continue to be managed exclusively by paperless-ngx.

The project is intended for users who want to browse their existing paperless-ngx collection in an alternative, document-oriented interface without replacing paperless-ngx data storage or permission management.

## Independent project

OrdnerBrowse is an independently developed, unofficial web interface for paperless-ngx with read-only access only.

This project is not affiliated with the paperless-ngx project or its contributors and is neither supported, reviewed, nor maintained by them. The name “paperless-ngx” is used solely to describe the compatibility of OrdnerBrowse with paperless-ngx.

## Project status

**Project status: Public Beta / Pre-1.0.** The current release version has completed the formal project release process. Until the first stable 1.0 release, user interface behaviour, configuration, and technical interfaces may still change.

OrdnerBrowse is developed as a hobby/best-effort project. No response times, maintenance periods, or service levels are guaranteed.

## Features

OrdnerBrowse provides an alternative, document-oriented interface for existing paperless-ngx collections.

The main features include:

- **Multi-level navigation:** Browse areas or storage paths, correspondents, document types, and documents.
- **Filters:** Correspondents and document types can be filtered by their labels within the selected navigation scope.
- **Document title search:** Search for terms in document titles within the selected navigation scope. Multiple search terms can be combined with AND (`&`) or OR (`|`).
- **Sorting:** Areas, correspondents, document types, and documents can be sorted.
- **Document preview:** Quickly preview the selected document using the thumbnail provided by paperless-ngx. The associated detail area can be scrolled vertically.
- **PDF-Quick Look:** Larger, multi-page PDF-Quick Look directly in OrdnerBrowse.
- **Document information:** Display important metadata such as title, correspondent, document type, area, date information, page count, file type, and document ID.
- **Tags and custom fields:** Display tags and custom fields stored in paperless-ngx.
- **Notes:** Display document notes stored in paperless-ngx, including user information and date, in both the document preview and PDF-Quick Look.
- **Display prefixes:** Configurable prefix rules can remove fixed prefixes as well as defined date and character patterns from the OrdnerBrowse display for areas or storage paths, correspondents, document types, and document titles. The underlying labels in paperless-ngx are not changed. Details of the supported rules and patterns are documented in `config/README_display-prefixes.md`.
- **Direct switch to paperless-ngx:** The selected document can be opened in the paperless-ngx frontend when needed.
- **Customisable interface:** Column widths can be changed or columns can be collapsed; PDF-Quick Look can be moved and resized.

All access to documents and functional data in paperless-ngx is read-only.

## Read-only principle

OrdnerBrowse accesses paperless-ngx in a read-only manner only.

Documents, metadata, correspondents, document types, storage paths, tags, custom fields, users, and permissions are not changed by OrdnerBrowse. paperless-ngx remains the sole source and manager of this data.

If a document needs to be edited, it can be opened in the paperless-ngx frontend from OrdnerBrowse. Changes are made there, not in OrdnerBrowse.

Local OrdnerBrowse functions such as sign-in, session management, personal Paperless connections, cache, or diagnostics do not change this functional read-only principle towards paperless-ngx.

## Container operation

OrdnerBrowse is intended to run as a containerised application.

The source tree contains the files and templates required for building, configuring, and starting the application in a controlled manner under `Containerbetrieb/`.

Runtime configuration, persistent data, and secrets are kept separate from the actual source code. Secrets do not belong in the source tree or the Compose file; they are supplied through the designated protected files or runtime paths.

Detailed documentation covering container operation, required configuration, persistence, the health check, and the intended operational procedures is provided in:

`Containerbetrieb/README_CONTAINERBETRIEB.md`

## Configuration

OrdnerBrowse separates public source code, runtime configuration, secrets, and persistent data.

Non-secret runtime values are configured for each installation. These include, for example, the connection to paperless-ngx, OIDC configuration, container parameters, and other operational values.

Secrets such as API tokens, client secrets, passwords, or private keys do not belong in source code, public configuration files, or the repository. They are supplied outside the source tree through the designated protected files and runtime paths.

Prefix rules can be configured for the OrdnerBrowse display. In addition to fixed prefixes, defined date and character patterns can be used. The rules affect only the visible display of areas or storage paths, correspondents, document types, and document titles; the original labels in paperless-ngx remain unchanged.

The neutral configuration template is located at:

`config/display-prefixes.example.json`

The supported prefix rules and pattern syntax, including date formats, `?` and `#` patterns, combinations, and examples, are documented in detail at:

`config/README_display-prefixes.md`

General configuration documentation is provided in:

`docs/en/CONFIGURATION.md`

The concrete runtime and container parameters are documented in detail in:

`Containerbetrieb/README_CONTAINERBETRIEB.md`

## Security

OrdnerBrowse separates application logic, runtime configuration, persistent data, and secrets.

Personal Paperless credentials are handled per user and are not passed to the browser. Access to paperless-ngx takes place server-side and is read-only only.

For sign-in during regular operation, OrdnerBrowse uses OpenID Connect (OIDC). Local development and test operation use separate sign-in mechanisms intended exclusively for the local development environment.

Session, token, and security-relevant runtime data are processed or stored outside the public source code.

Secrets such as API tokens, client secrets, passwords, or private keys must not be included in source code, public configuration files, logs, or the repository.

Detailed security guidance and information about reporting security issues are provided in:

`SECURITY.en.md`

## Support

OrdnerBrowse is a hobby/best-effort project. No response times, fix deadlines, maintenance windows, or availability guarantees are provided.

Reproducible problems, display issues, container-operation errors, and specific documentation problems can be reported.

Passwords, API tokens, 2FA codes, secret contents, private keys, original documents, productive runtime files, or other confidential content must not be submitted.

Further guidance on support requests and useful information is provided in:

`SUPPORT.en.md`

## Contributing

Contributions to OrdnerBrowse are welcome if they are compatible with the project's existing architecture, security, and read-only principles.

Changes should be as small and traceable as possible and limited to a clear functional or technical purpose. Functional changes must be covered by suitable tests or reproducible validation steps.

Contributions must not contain passwords, API tokens, 2FA codes, secret contents, private keys, original documents, productive runtime files, or confidential real-world data. Examples and tests must use neutral placeholders.

New third-party dependencies must be checked for origin, version, licence, and redistribution terms.

Further guidance on contributions, tests, releases, and licensing is provided in:

`CONTRIBUTING.en.md`

## License

OrdnerBrowse is distributed under the **GNU Affero General Public License v3.0 or later (`AGPL-3.0-or-later`)**.

The complete licence text is provided in:

`LICENSE`

OrdnerBrowse also uses and distributes third-party components with their own licence terms, including Bootstrap, Popper, PDF.js, and Microsoft components for OpenID Connect.

An overview of the third-party components used and their corresponding licence texts is provided in:

`THIRD_PARTY_NOTICES.en.md`

The complete bundled third-party licence texts are stored under:

`LICENSES/`

## Language

OrdnerBrowse project documentation is provided in German and English.

The German README is located at:

`README.md`

The English version is located at:

`README.en.md`

Further German- and English-language documentation is located under:

`docs/de/`

`docs/en/`

Both language versions are intended to represent the same functional and technical project state.
