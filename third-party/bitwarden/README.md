# Bitwarden CLI OSS component

ServerDesk distributes unmodified **OSS** Bitwarden CLI 2026.10.0 executables as a separate process, not the Bitwarden-licensed enterprise build. This component is copyright Bitwarden Inc. and contributors, GPL v3; ServerDesk's own code remains MIT. This integration is not endorsed by Bitwarden.

Pinned upstream source: [clients at b449ac1d255784380d75ef026343198891286be7](https://github.com/bitwarden/clients/tree/b449ac1d255784380d75ef026343198891286be7), official release [cli-v2026.10.0](https://github.com/bitwarden/clients/releases/tag/cli-v2026.10.0). Full GPL text and upstream licensing notice are retained byte-for-byte beside this file.

The distribution includes corresponding OSS clients source, with build scripts, lockfiles and notices, under components/bitwarden/SOURCE.tar.gz. Files remain byte-for-byte upstream; scripts/prepare-bitwarden-source.py excludes the commercially licensed bitwarden_license subtree and repacks deterministically. Both original and OSS-only archive hashes are pinned. See apps/cli/package.json and upstream workflows for OSS build commands. Dependencies and the included Node runtime retain their own licenses; upstream source includes dependency/build metadata and additional notices. Downloaded binaries/source archives are build inputs cached outside Git, never application credentials.

Use scripts/prepare-bitwarden.ps1 to prepare a Windows/Linux component. The script verifies both archive and executable hashes before copying. No runtime update/download of executables is performed by the application. A new component version requires reviewed manifest hashes and a ServerDesk release.
