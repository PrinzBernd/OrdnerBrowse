# Configuration

## Principle

OrdnerBrowse separates source code, public configuration definitions and templates, installation-specific runtime configuration, secrets, and persistent runtime data.

The public source tree may contain non-secret default values, configuration structures, and neutral example templates. They describe the supported settings without defining productive or confidential values.

Installation-specific runtime values are supplied according to their purpose through the designated configuration files, environment values, or external runtime paths.

Secrets and other confidential values do not belong in the public source tree or the repository. Persistent runtime data is also kept separate from the actual source code.

Public examples use neutral placeholders only and are not intended to be directly usable as productive configuration.

## Display prefixes

Prefix rules can be configured for the OrdnerBrowse display for areas or storage paths, correspondents, document types, and document titles.

The rules change the visible display in OrdnerBrowse only. The original labels in paperless-ngx remain unchanged.

The configuration uses four sections:

- `Areas`
- `Correspondents`
- `DocumentTypes`
- `Documents`

Fixed prefixes, defined date placeholders in year-first or day-first notation, and freely combinable character patterns are supported.

Within character patterns, `?` represents exactly one Unicode letter or one ASCII digit from `0` to `9`; `#` represents exactly one ASCII digit from `0` to `9`. Fixed characters can be specified at the required position within a pattern.

A rule applies only at the beginning of a label. If several rules match, the longest matching prefix is removed. Characters such as underscores, hyphens, or dots must be part of the rule if they are also to be removed from the visible display.

The complete list of supported date formats, the exact `?`/`#` syntax, combination rules, and positive and negative examples are documented in detail only in `config/README_display-prefixes.md`.

Invalid individual rules are ignored. If an updated configuration file as a whole is invalid, the last valid configuration remains active.

If the configuration is missing or contains no rules, the labels remain unchanged.

The neutral public template is located at:

`config/display-prefixes.example.json`

Detailed documentation with examples is provided at:

`config/README_display-prefixes.md`

The productive configuration file is kept outside the public source tree and is supplied for container operation through the designated runtime path.

## Runtime paths

OrdnerBrowse keeps installation-specific configuration, secrets, and persistent runtime data outside the actual source tree.

The concrete runtime root, meaning the base directory of the respective runtime installation, is defined for each installation. General project documentation does not use concrete host- or installation-specific directories for this purpose.

Different types of data are kept separate within the runtime environment. These include in particular:

- configuration files,
- secret and protection files,
- navigation and cache data,
- protected user-specific connection data,
- Data Protection keys,
- OIDC diagnostic data,
- performance diagnostic data.

A further distinction is made between areas mounted read-only and writable persistent runtime areas. Configuration and secrets are made available to the container only with the intended access mode.

The concrete directory structure, `WEBUI_RUNTIME_ROOT`, bind mounts and their read/write permissions, and the different path resolutions for primary and fallback operation are authoritatively documented in:

`Containerbetrieb/README_CONTAINERBETRIEB.md`

## Secrets and OIDC

Secrets such as API tokens, client secrets, passwords, or private keys must not be part of the public source tree, public configuration files, or the repository.

They are supplied outside the source tree through the designated protected runtime paths or secret files.

For sign-in during regular operation, OrdnerBrowse uses OpenID Connect (OIDC). The installation-specific OIDC values required for this are supplied through the designated runtime configuration.

Confidential OIDC values, in particular client secrets, are not stored in public example or project files.

The OIDC provider performs authentication. Functional user and permission information for access to documents continues to come from paperless-ngx.

Local development and test operation use separate sign-in mechanisms intended exclusively for the local development environment.

Further security-related guidance is provided in:

`SECURITY.en.md`

The concrete runtime and container parameters are documented in:

`Containerbetrieb/README_CONTAINERBETRIEB.md`

## Public examples and productive configuration

Public example and template files show the supported structure, allowed keys, and neutral example values of a configuration. They serve as a starting point for an installation and contain no productive or confidential values.

These include in particular:

- `Containerbetrieb/.env.example` as the public template for non-secret installation-specific container and runtime parameters,
- `config/display-prefixes.example.json` as a neutral template for display-prefix configuration.

The respective productive configuration is supplied per installation outside the public source tree or through the designated external runtime paths.

In particular, a productive `.env` file, secret files, personal credentials, and other confidential runtime values do not belong in the repository or a public source tree.

Public templates may therefore be copied and adapted for an installation; the resulting productive files remain part of the respective runtime environment and are not added to the public source tree.

Changes to public templates must be designed so that they continue to contain only neutral and publishable content.
