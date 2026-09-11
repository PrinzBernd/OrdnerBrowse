# Security

## Security principles

OrdnerBrowse is functionally read-only towards paperless-ngx.

paperless-ngx remains the sole source of data, documents, users, and permissions. Writing Paperless API methods for functional data, documents, users, or permissions are not part of the architecture and must not be introduced.

Secrets, personal credentials, and productive runtime data are kept separate from public source code and public project artefacts.

Passwords, API tokens, client secrets, 2FA codes, private keys, and comparable confidential values must not be included in the public source tree, public configuration files, logs, or publication artefacts.

Public examples, tests, and documentation use neutral or neutralised values only and must not contain confidential real-world data.

Local OrdnerBrowse functions such as sign-in, session management, personal Paperless connections, cache, or diagnostics can manage their own runtime data. They do not change the functional read-only principle towards paperless-ngx.

## Reporting a security issue

Suspected security issues or vulnerabilities should be described as concisely and reproducibly as possible.

Confidential details of a suspected vulnerability should not be published publicly in issues, discussions, or other publicly visible areas.

Only neutral or neutralised example values may be used in a report. Passwords, API tokens, 2FA codes, secret contents, private keys, original documents, productive runtime files, or other confidential real-world data must not be submitted.

GitHub Private Vulnerability Reporting is enabled for confidential security reports in the public OrdnerBrowse repository. Suspected vulnerabilities that require confidential handling should be reported through this private GitHub reporting channel.

Additional security contacts or other private reporting channels will only be stated in this file once they have actually been established, are reachable, and are documented in the public repository.

## Required information

A security report should, as far as possible without confidential content, include the following information:

- the affected OrdnerBrowse version,
- the affected feature or application area,
- a short description of the suspected security issue,
- reproducible steps that demonstrate the behaviour,
- the expected and actually observed behaviour,
- the observed or suspected security impact,
- where safely possible, a technical assessment of the issue.

All information must be neutralised sufficiently to ensure that it contains no passwords, tokens, secrets, private keys, personal data, original documents, or productive runtime data.

Log excerpts, configuration examples, or screenshots may only be used after confidential and productive information has been completely removed.

As a rule, only the smallest amount of information required to describe the issue reproducibly should be submitted for investigation.

## Content that must not be submitted

Confidential or productive content must not be submitted or published regardless of the reporting channel used.

This includes in particular:

- passwords, API tokens, and 2FA codes,
- client secrets, other secret contents, and private keys,
- original documents or confidential content copied from them,
- productive configuration and runtime files,
- personal or other confidential real-world data,
- unredacted logs, screenshots, or configuration excerpts containing such information.

Only neutral or fully neutralised substitute values may be used for examples, reproduction steps, and technical explanations.

If there is uncertainty about whether information is confidential or productive, it should not be submitted.

## Read-only and secret boundaries

paperless-ngx remains the sole source of data, documents, users, and permissions for the functional content displayed by OrdnerBrowse.

OrdnerBrowse accesses this information in a read-only manner only. Writing Paperless API methods for functional data, documents, users, or permissions are not part of the intended architecture.

Local state and runtime data of OrdnerBrowse, such as session data, personal Paperless connections, cache, or diagnostic data, are separate from this. They must not bypass the functional read-only principle towards paperless-ngx.

OIDC secrets and other confidential access values are supplied outside the public source code and public configuration files.

Secret files and other runtime data requiring protection must be mounted through the designated protected runtime paths and protected from unauthorised access using appropriate permissions.

Secrets must neither be passed to the browser nor disclosed in logs, diagnostic output, or public artefacts.

## Handling model

OrdnerBrowse is developed and maintained as a hobby/best-effort project.

No response times, handling deadlines, remediation deadlines, or service levels are guaranteed for security reports.

Incoming security reports should nevertheless be reviewed according to their plausible impact and urgency and appropriately prioritised over ordinary bug or feature reports.

A report or its review does not automatically mean that a specific change, remediation, or release will take place within a defined period.

If resolving a security issue requires a change to source code or published artefacts, that change remains subject to the project's applicable validation, test, and release rules.
