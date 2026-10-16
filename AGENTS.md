# Working on ServerDesk

## Repository

- `src/ServerDesk.Core`: protocol-independent models, validation and state.
- `src/ServerDesk.Infrastructure`: persistence, credentials and protocol implementations.
- `src/ServerDesk.App`: Avalonia UI and platform integration.
- `tests/ServerDesk.Tests`: unit and bounded loopback tests.
- `tools`: opt-in acceptance harnesses; `scripts`: build, packaging and lab entry points.
- Current guides live in `docs/`; historical release notes in `docs/releases/`; verification/audit records in `docs/reports/`.

## Git workflow

The maintainer wants ongoing work versioned in Git. Inspect `git status` before editing. Complete and validate each requested logical change, then create a local commit with a concise English message. Routine local commits need no extra confirmation. Do not include unrelated user edits. Do not amend published history, move existing release tags, or push without the user's publication instruction.

`main` is the primary branch. The imported `v0.31.1` tag matches the frozen source release; earlier releases have documents and local packages but no fabricated Git history. Subsequent public versions use `vMAJOR.MINOR.PATCH` tags and `Directory.Build.props` as the version source. Do not overwrite frozen packages under `artifacts/releases/`.

The maintainer expects both commits and usable local releases. At the start, run `scripts/check-release-state.ps1` to identify changes ahead of the current release. After a requested batch of user-visible application changes, prepare the next patch/minor version, update the changelog/current guides, complete the release gates, assemble versioned Windows/Linux/source packages, commit the validated source and create its annotated tag. Finish with `scripts/check-release-state.ps1 -RequireCurrentRelease` and link the installer. Intermediate logical commits are encouraged; they do not replace the release. Documentation-only maintenance need not create an application release. An explicit user instruction to work only on source overrides this cadence. Preparing a local release does not authorize pushing or publishing remotely.

## Validation and content

- Start with `pwsh -File scripts/check-repository.ps1`.
- Restore locked dependencies, build `ServerDesk.sln` in Release, and run relevant tests. See `CONTRIBUTING.md` and `.github/workflows/ci.yml` for exact commands.
- Keep `packages.lock.json` files tracked and update them deliberately when dependencies change.
- Never commit local SDKs, downloaded tools, generated packages, databases, logs, credentials or private keys. Lab passwords in source are documented disposable test constants.
- Reuse current stopped harness builds where practical. Keep selected final evidence in `artifacts/reports/` and clean disposable output with `scripts/clean-artifacts.ps1`; see `docs/artifacts.md`. Preserve frozen releases.
- Keep upstream notices and branded assets byte-for-byte where hashes are pinned. Respect `.gitattributes` and `.editorconfig`.
- Run integration/hardware tests only against explicit disposable fixtures, never against a maintainer's saved production connections or credentials.
- Keep Markdown links and packaging documentation paths valid after moving files. Packaging source exports must include GitHub/repository configuration.
