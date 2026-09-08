# Publication

## Version scheme

Published versions of OrdnerBrowse receive an explicitly defined version number. A specific version number may only be described as a published version once the designated release process has been completed in full.

Release candidates use the suffix `-rc.N`, where `N` is the sequential number of the release candidate.

Example:

`vX.Y.Z-rc.N`

Existing historical version identifiers with suffixes such as `-r1` remain unchanged and are not renamed retroactively.

The version number alone says nothing about release status. The documented status and corresponding evidence are always decisive.

## Release status

A version number alone does not establish release status.

A source state can, for example, be planned, reconstructed, changed, unbuilt, tested, or prepared as a release candidate without therefore already being formally released.

A version is considered formally released only once all validations and approval steps designated for that state have been completed successfully and the release has been explicitly documented.

A public publication additionally requires all designated publication components to have been provided completely and consistently.

Release candidates are used for final validation before a possible stable release and are not equivalent to a formally published stable version.

The documented status and corresponding evidence are always decisive, not merely the version number or an artefact filename.

## Container artefacts

The OrdnerBrowse build and reference chain considers two target CPU architectures:

- **ARM64**
- **AMD64**

Container artefacts for both target architectures are built and validated specifically for their architecture. The same source state intended for release is the basis in each case.

Successfully creating and validating an architecture-specific container artefact does not by itself mean that the artefact is publicly published or made available through a container registry.

A public registry strategy and the concrete form in which container artefacts are provided are defined separately.

A common **multi-architecture image** combining several CPU architectures under one image designation has not currently been defined.

Likewise, no `latest` tag strategy has currently been defined. A `latest` tag must therefore not be used or assumed to exist without a previously explicitly defined publication rule.

Until corresponding decisions have been made and documented, architecture-specific artefacts must be unambiguously attributable to their version and target architecture.

## Required publication artefacts

A public release of OrdnerBrowse includes at least:

- the unambiguously versioned and released source state,
- the public project documentation belonging to the released state,
- `LICENSE`,
- `THIRD_PARTY_NOTICES.md`,
- `THIRD_PARTY_NOTICES.en.md`,
- the bundled third-party licence texts under `LICENSES/`,
- a public changelog consistent with the released state,
- the required evidence that the build, test, and release gates designated for that state have been completed successfully.

All publication components must be attributable to the same released project state and be consistent with respect to version information, licence information, and documentation.

Internal validation logs do not necessarily have to be published themselves. What matters is that the required validations were performed in a revision-safe manner and successful completion of the respective release gate was evidenced.

If container artefacts are made publicly available, they too must be unambiguously attributable to the released project state and the respective target architecture.

## Release gates

Before a formal release, all release gates designated for the respective state must have been completed successfully and documented in a traceable manner.

These include at least:

- the source state intended for release is unambiguously identified and assigned to its version number,
- the designated builds have completed successfully,
- the designated automated regression tests have completed successfully,
- designated container artefacts have been built and validated successfully for their respective target architecture,
- the publication components are complete and assigned to the same project state,
- version information, public documentation, changelog, and licence information are mutually consistent,
- the source state and publication artefacts intended for release contain no productive secrets, personal credentials, productive runtime files, or other confidential content.

A technically successful build or test run does not by itself constitute a release.

Formal publication takes place only after successful completion of all designated validations and an explicitly documented approval.

If any designated release gate is not passed or required evidence is missing, the respective state must not be described as formally published.

## Internal evidence

Internal validation and approval evidence supports revision-safe development and publication processes.

This can include in particular:

- build and test logs,
- checksums and file lists,
- evidence for container builds and target architectures,
- internal release and fallback evidence,
- local development and reference paths,
- system-specific operational and working directories.

This internal evidence does not have to be part of the public publication.

Public project documentation should not publish local user paths, system-specific internal working directories, productive runtime paths, confidential operational information, or internal release logs unless they are required to use the project.

Instead, public documentation describes the prerequisites, configuration rules, licence information, version information, and the respective intended form of publication that are relevant to users.

Internal evidence remains unaffected and continues to be maintained according to the validation and approval rules defined for the project.
