# Support

## Support model

OrdnerBrowse is developed and maintained as a hobby/best-effort project.

No response times, handling or fix deadlines, maintenance windows, availability guarantees, or service levels are provided.

Support requests and bug reports are reviewed as time permits. This does not create an entitlement to handling a specific report, implementing a requested feature, or providing a fix within a particular period.

Security issues are handled separately. The applicable guidance is provided in:

`SECURITY.en.md`

## What can be reported

In particular, reproducible problems concerning OrdnerBrowse itself or the documentation belonging to the project can be reported.

Examples include:

- reproducible functional errors in OrdnerBrowse,
- display or interaction errors in the web interface,
- problems with the intended container operation,
- errors or contradictions in the public project documentation,
- reproducible configuration problems using the settings and templates intended by the project.

A report should, where possible, concern one clearly delimited problem and describe it in a way that allows the behaviour to be reproduced using neutral example values.

Requests for new features or changes can also be described. This does not create an entitlement to implementation or inclusion in a future version.

Suspected security issues do not belong in ordinary support reports. The guidance in the following file applies:

`SECURITY.en.md`

## Helpful information

A support report should, where relevant to the respective problem, include as much of the following information as is useful:

- the OrdnerBrowse version used,
- the affected feature or area,
- a short description of the problem,
- the expected behaviour,
- the actually observed behaviour,
- reproducible steps that demonstrate the problem,
- where helpful, information about the affected operating mode or target architecture.

Examples and reproduction steps must use neutral or neutralised values only.

Log excerpts, screenshots, or configuration examples may only be attached after personal, productive, confidential, and secret content has been completely removed or neutralised.

Only the amount of information required to describe the problem reproducibly should be submitted.

## What must not be submitted

Support reports must not contain confidential, productive, or secret content.

This includes in particular:

- passwords, API tokens, and 2FA codes,
- client secrets, other secret contents, and private keys,
- original documents or confidential content copied from them,
- productive configuration and runtime files,
- personal or other confidential real-world data,
- unredacted logs, screenshots, or configuration excerpts containing such information.

Only neutral or fully neutralised substitute values may be used for examples, reproduction steps, and technical explanations.

If there is uncertainty about whether information is confidential or productive, it should not be submitted.

## Scope

Support for OrdnerBrowse concerns problems that can be attributed to the project itself or to the documentation and configuration it provides.

OrdnerBrowse depends on external components and services for its operation. Problems in such components can affect the use of OrdnerBrowse without the cause lying in OrdnerBrowse itself.

These can include problems in the following areas in particular:

- paperless-ngx,
- OIDC provider,
- reverse proxy,
- container runtime and host system,
- network, DNS, or TLS configuration,
- other third-party components in use.

If the cause is not initially clear, the behaviour can still be described as a support issue. The information must continue to comply with the rules in this file and must not contain confidential or productive content.

If review shows that the cause lies outside OrdnerBrowse, the responsible component and its documentation or support may be referenced. OrdnerBrowse cannot guarantee remediation of errors in external projects or services.

Integrating or being compatible with an external component does not mean that OrdnerBrowse develops, operates, or supports that component itself.
