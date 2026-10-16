# Security reporting and support

The current public beta is **0.31.1**. Earlier development builds are superseded. There is no long-term support commitment for beta releases; use the most recent published version.

Report a suspected vulnerability privately to the maintainer who supplied your release. A dedicated reporting address and public repository have not been configured in this source snapshot. Do not post passwords, private keys, credential stores, databases, usable connection details or a proof of concept against another person's server in a public issue. A report should describe the affected version, prerequisites, impact and a minimal reproduction against your own test environment.

This release has automated tests and a security review, not a penetration-test certification. See `docs/known-issues.md` and `docs/privacy.md`.

SSH host keys and RDP certificates must be verified. VNC has no encryption or server identity verification: use a trusted VPN or an existing secure tunnel. Automatic update downloads are not implemented; install updates from the same trusted publisher and verify their checksum. A checksum supplied beside a download detects corruption; it does not independently authenticate the publisher.
