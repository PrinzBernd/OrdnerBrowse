# Contributing

## Principles

Contributions to OrdnerBrowse should be small, traceable, and limited to a clearly defined functional or technical purpose.

Changes should follow the existing project behaviour, architecture, and public project documentation.

Before making a change, verify that it is compatible with the project's existing architecture, security, read-only, and publication rules.

Unsubstantiated assumptions about the intended behaviour should not form the basis of a change. If the intended behaviour is unclear, the functional or technical objective should be clarified first.

## Scope of changes

Changes should be root-cause oriented and minimally invasive wherever possible. They should be limited to the functional or technical area actually affected.

Unrelated changes, larger refactoring, or additional functional changes should be handled separately.

Existing behaviour outside the intended scope of the change should not be altered without a traceable reason.

A change should affect only the files and components actually required for its purpose.

## Read-only guarantee

OrdnerBrowse must remain functionally read-only towards paperless-ngx.

In particular, no writing Paperless API methods may be introduced for documents, metadata, users, or permissions.

paperless-ngx remains the sole source of data, documents, users, and permissions.

Changes to local session, cache, diagnostic, or other technical runtime data of OrdnerBrowse must be considered separately. They must not remove or bypass the functional read-only boundary towards paperless-ngx.

## Secrets and real data

Contributions must not contain passwords, API tokens, 2FA codes, client secrets, other secret contents, private keys, original documents, productive configuration or runtime files, personal real-world data, or other confidential content.

This also applies to source code, tests, example files, configuration templates, log excerpts, screenshots, and any other files submitted with a contribution.

Examples, tests, and documentation must use neutral or fully neutralised placeholders and example data only.

If there is uncertainty about whether content is confidential or productive, it should not be included in a contribution.

## Tests

Changes should be covered by suitable tests.

Existing automated tests must continue to pass. If new or changed behaviour is introduced, the corresponding tests should be added or adapted wherever the behaviour can be tested automatically.

Tests should cover the actual changed functionality and in particular ensure that existing behaviour outside the intended scope is not changed unintentionally.

Changes to the integration with paperless-ngx must additionally respect the project's functional read-only boundary and must not require or introduce writing Paperless API access.

A successful test run alone does not constitute formal approval or publication of a version.

## Versions and releases

Version numbers and release status must not be increased or described as released solely because of a source change.

A higher version number, a successful build, or a passed test run does not by itself constitute formal approval.

Release candidates, changed working states, and other states that have not yet been fully released must remain identifiable as such.

A stable version is considered formally released only after all designated validation and approval steps have been completed and the release status has been explicitly documented.

Contributions must therefore neither present an unreleased state as a release nor pre-empt the documented release process through a version change alone.

## Third-party code

New or changed third-party dependencies must be checked for origin, version, licence, redistribution terms, and required licence notices.

Only dependencies whose use and redistribution are compatible with the project licence and intended publication may be added.

Required licence notices and licence texts must be updated completely and traceably together with the respective change.

If a change affects `THIRD_PARTY_NOTICES.en.md` or licence texts maintained under `LICENSES/`, those files must be updated accordingly.

Third-party code or external content must not be added to the project without clarified origin and licence terms.

## Licensing of contributions

Contributions to OrdnerBrowse must be compatible with the project licence `AGPL-3.0-or-later`.

Anyone submitting a contribution must be authorised to contribute their own source code, documentation, and other original content under terms that allow the overall project to be published under `AGPL-3.0-or-later`.

Contributions must not contain additional licence terms, usage restrictions, or other reservations of rights that conflict with publication or redistribution of the overall project under `AGPL-3.0-or-later`.

For included third-party components, the requirements in the `Third-party code` section also apply.
