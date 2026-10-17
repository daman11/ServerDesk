# Module boundaries — 2026-10-17

Small visual development increment after 0.41.0. Application version, existing tags and frozen release packages remain unchanged.

- Frame navigation groups independently: connections/favorites/recent, transfers, network tools and plugins; application settings stay at the bottom. Keep the 48 px rail and its accessible button names, with scrolling available in a short workspace.
- Sidebar modules have a contrasting header surface, strong bottom border, contextual icon, mixed-case title and a short localized purpose. The header grows with wrapping rather than relying on a fixed height.
- Command panels identify themselves as a plugin and explain their purpose. A stronger divider separates the panel from sessions. Network tool forms have a separate header surface and a Network tools scope label above the individual tool name.
- Session toolbars visually separate Terminal/SFTP from optional plugins, and dock tab strips have a bottom rule. Decorative plugin groups/separators follow enabled entry points and disappear when none remain; saved settings still control plugin lifecycles.
- Complete icon metadata for the existing Show all settings action found by the focused presentation audit.

Validation uses the existing bounded UI harness with disposable databases and no real server sessions: presentation consistency (117 production action labels, 338 menu items and 288 buttons) for protocol/tool headers and commands; connection browser navigation and 220 px/font16 layouts in PL/EN Graphite/Light. The commands scenario covers list/details/back, copy/favorites and compact font13/16 with expanded/collapsed profile options. Render inspection also checks that the Plugin badge has its own header column alongside management actions. Eight filtered localization checks cover catalog parity and placeholders. Incremental Release build, repository/link and whitespace checks apply to this development step.

Selected final renders, logs, TRX and a result summary are retained under `artifacts/reports/module-boundaries-2026-10-17/`; disposable UI databases are removed after verification. The current development executable path is recorded in `build-directory.txt` there.

No full matrix, release packaging, VMware, native Linux acceptance or physical-monitor testing is claimed. This change does not add module capabilities or change transfer/session behavior.
