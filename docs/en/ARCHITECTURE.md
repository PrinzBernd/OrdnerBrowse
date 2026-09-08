# Architecture

## Role of OrdnerBrowse

OrdnerBrowse is an independent, independently developed, unofficial web interface for paperless-ngx.

The application presents existing documents and related information from paperless-ngx in an alternative, document-oriented interface.

paperless-ngx remains the sole source of data, documents, users, and permissions. OrdnerBrowse does not replace paperless-ngx data storage or permission management.

## Read-only principle

OrdnerBrowse is functionally read-only towards paperless-ngx.

The application reads documents, metadata, correspondents, document types, storage paths, tags, custom fields, notes, and user and permission information from paperless-ngx, but does not change this data.

Writing Paperless API methods for functional data, documents, users, or permissions are not part of the architecture and must not be introduced.

Local OrdnerBrowse functions such as sign-in, session management, personal Paperless connections, cache, or diagnostics can manage their own runtime data. They do not change the functional read-only principle towards paperless-ngx.

This principle is a central architecture and security boundary of the project.

## Main technical components

OrdnerBrowse mainly consists of three technical project areas:

- `WebUI.Web`: web application and user interface. This area provides the web interface and contains the web functions required for application operation.
- `WebUI.Infrastructure`: technical infrastructure and integration with external services, in particular paperless-ngx.
- `WebUI.Tests`: automated project-related tests that safeguard the intended behaviour and detect regressions.

The technical project name `WebUI` continues to be used where it is part of source code, project files, paths, or container operation.

The public product name is **OrdnerBrowse**.

## Configuration and runtime data

OrdnerBrowse separates public source code, non-secret runtime configuration, secrets, and persistent runtime data.

Non-secret configuration values and public configuration templates may be part of the source tree. These include, for example, neutral templates and display settings.

Productive secrets such as API tokens, client secrets, passwords, or private keys do not belong in source code or the repository. They are supplied outside the public source tree through the designated protected runtime paths.

Persistent application runtime data is also kept outside the actual source code. This keeps the program state, configuration, secrets, and data created or required at runtime technically separate.

Public examples and templates use neutral placeholders only and must not contain productive or confidential content.

The concrete configuration options are described in `docs/en/CONFIGURATION.md`, and the operational runtime paths are described in `Containerbetrieb/README_CONTAINERBETRIEB.md`.

## Container operation

OrdnerBrowse is intended to run as a containerised application.

The container image contains the application and the program components required for its operation. Installation-specific configuration, secrets, and persistent runtime data are not built permanently into the image; they are supplied at runtime through the designated external paths or mounts.

This allows the same application state to be run with different installation-specific configurations without changing the actual source or program state.

Container operation considers two CPU architectures:

- **ARM64** for the local ARM64 reference environment,
- **AMD64** for the DiskStation reference environment.

The same source state is used for both architectures. The respective container images are nevertheless built and tested specifically for their architecture.

The container provides its own health check, which can be used to verify the technical state of the running application.

The concrete container structure, configuration parameters, mounts, persistence, and the intended build, start, and validation procedures are documented in detail in:

`Containerbetrieb/README_CONTAINERBETRIEB.md`

## OIDC and reverse proxy

For sign-in during regular operation, OrdnerBrowse uses OpenID Connect (OIDC).

Authentication is performed by an external OIDC provider. OrdnerBrowse handles the OIDC communication required for sign-in but does not manage its own central user or permission source.

User and permission information for access to documents continues to come from paperless-ngx.

In the intended production operation, OrdnerBrowse can run behind a reverse proxy. The reverse proxy provides external access to the application and can in particular handle HTTPS termination, forwarding, and hostname assignment.

The OIDC provider, reverse proxy, and OrdnerBrowse remain technically separate components, each with its own responsibility.

Local development and test operation use separate sign-in mechanisms intended exclusively for the local development environment.
