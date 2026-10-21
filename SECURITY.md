# Security reporting and support

ServerDesk is in public beta. The source version is defined in [Directory.Build.props](Directory.Build.props); development commits may be ahead of the last packaged release. There is no long-term support commitment for beta releases; use the most recent published version.

Report a suspected vulnerability privately to the maintainer who supplied your release. The public repository is [daman11/ServerDesk](https://github.com/daman11/ServerDesk); a dedicated private reporting address is still to be selected. Do not post passwords, private keys, credential stores, databases, usable connection details or a proof of concept against another person's server in a public issue. A report should describe the affected version, prerequisites, impact and a minimal reproduction against your own test environment.

This release has automated tests and a security review, not a penetration-test certification. See `docs/known-issues.md` and `docs/privacy.md`.

SSH host keys and RDP certificates must be verified. VNC has no encryption or server identity verification: use a trusted VPN or an existing secure tunnel. From 0.45.0, optional update checks and user-confirmed Windows Setup installation use the pinned GitHub repository, HTTPS and mandatory asset SHA-256/size checks. There is no separate publisher signing key or Authenticode; a repository-account compromise remains a threat. See [update trust, privacy and recovery](docs/updates.md). A checksum supplied beside a download detects corruption; it does not independently authenticate the publisher.
