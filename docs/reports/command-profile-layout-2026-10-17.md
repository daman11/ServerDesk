# Command profile layout — 2026-10-17

Focused UI increment after 0.41.0. The system selector now occupies a full row; shell and version use separate rows. Expanding the profile allocates the remaining panel height to its fields while reserving room for command results and operation notices. The expander heading and current environment summary stay outside the scrolling fields. Long system names have a tooltip, and the dropdown can show more entries.

The isolated `--command-profile` scenario checks PL/EN at 1280×800 and 920×620 with interface fonts 13/16: the full Red Hat Enterprise Linux name, open dropdown rendering, focus scrolling to version/system, visible results including a copy notice, resize/collapse retention and persisted manual profile preferences. It uses disposable data and an in-memory credential fixture, with no remote sessions. Selected renders are retained in `artifacts/reports/command-profile-layout-2026-10-17/`.

Validation: incremental Release build and repository/whitespace checks. No full application matrix, version bump, release tag or package rebuild.
