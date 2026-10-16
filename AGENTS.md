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

`main` is the primary branch. The imported `v0.31.1` tag matches the frozen source release; earlier releases have documents and local packages but no fabricated Git history. Subsequent public versions use `vMAJOR.MINOR.PATCH` tags and `Directory.Build.props` as the version source. Commit changes independently of release numbering. Do not overwrite frozen packages under `artifacts/releases/`.

## Validation and content

- Start with `pwsh -File scripts/check-repository.ps1`.
- Restore locked dependencies, build `ServerDesk.sln` in Release, and run relevant tests. See `CONTRIBUTING.md` and `.github/workflows/ci.yml` for exact commands.
- Keep `packages.lock.json` files tracked and update them deliberately when dependencies change.
- Never commit local SDKs, downloaded tools, generated packages, databases, logs, credentials or private keys. Lab passwords in source are documented disposable test constants.
- Keep upstream notices and branded assets byte-for-byte where hashes are pinned. Respect `.gitattributes` and `.editorconfig`.
- Run integration/hardware tests only against explicit disposable fixtures, never against a maintainer's saved production connections or credentials.
- Keep Markdown links and packaging documentation paths valid after moving files. Packaging source exports must include GitHub/repository configuration.
