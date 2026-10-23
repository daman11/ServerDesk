# ServerDesk 0.49.1 publication verification

Date: 2026-10-23. Public Beta. Scope: connection-row presentation patch, versioned packaging and authorized GitHub publication.

## Existing functional evidence

The connection-row patch passed 64 headless layout cases: SSH/RDP; connected/disconnected and favorite states; 180/300-pixel row widths; font sizes 13/16; address details; Dark/Light. Protocol and name origins stay fixed, and the status marker does not overlap the server icon/name. Incremental Windows application and harness builds passed with zero warnings/errors. These checks were completed before the publication request.

The [0.47](release-verification-0.47.0.md), [0.48](release-verification-0.48.0.md) and [0.49](release-verification-0.49.0.md) reports retain their own functional/security evidence. They are not fresh results for 0.49.1. The maintainer explicitly requested no repeat of the full functional suite for this publication. No new unit matrix, UI matrix, installer workflow, VM, real vault-account or RDP/hardware acceptance run is claimed.

## Publication checks

The release procedure scans all Git history and the exact source/distribution snapshot with Gitleaks 8.30.1 using redacted reports. Existing narrowly scoped fixture/prose exceptions remain; no broad rule or directory exclusions are added to the scanner. Local Codex attachments are ignored, and the source export is restricted to the repository allowlist. Local SDKs, cached authentication, account data, databases, logs and private key files are excluded from distribution.

Fresh self-contained Windows/Linux payloads are built from the final release commit. Content checks cover version/commit metadata, source equivalence, private/build-file exclusions, pinned component binary/source/notices, package contents and SHA-256 digests. Uploaded assets are checked against local sizes/digests before making the draft public. Final outcomes and the exact release commit are recorded in the frozen VERIFICATION.md accompanying the packages.

This bounded scan reports detected patterns and reviewed contents; it cannot prove that no unknown secret format exists. Disposable credentials and generated key wrappers in opt-in lab tests are documented fixtures, not user account data. The separate Bitwarden component remains at its pinned 0.49.0 version/hashes.

## Remaining limits

Packages are unsigned Public Beta. Prior real vault-account, RDP/clipboard/physical-input/DPI/network and clean-desktop acceptance limits remain open. The official vault client retains its own local account/cache state as documented; publication scans do not inspect the maintainer's vault or credential stores.

[Release notes](../releases/release-0.49.1.md), [known issues](../known-issues.md), [privacy](../privacy.md).
