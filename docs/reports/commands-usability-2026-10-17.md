# Commands usability — 2026-10-17

Focused development increment after 0.41.0. Application version, release tags and frozen packages remain unchanged.

- Keep search/source/category/sort/risk controls outside the scrolling system-profile area. Add a risk filter and reset/inline clear actions that restore focus without changing the source, system profile or saved preferences. The risk filter is transient panel state.
- Explain empty searches, custom commands, favorites, recent commands/history opt-out and unmatched profiles. Actions clear filters, open the existing editor, return to the reference or explicitly enable the existing history feature. Locked custom libraries retain access to built-ins.
- Search Enter flushes the debounce before opening details; Down focuses results, Escape clears the query, and Ctrl+F returns from details to search. None dispatches a command. Returning to the list retains selection and filters.
- Track the applied search text and cancel pending refreshes. A deferred TextChanged event following immediate Enter/reset must not rebuild the virtualized list during a click or erase a newly entered parameter; the UI scenario retains a service-parameter draft beyond the debounce interval.
- Cards separate title/favorite marker, description, template, category/shell and a textual risk badge. Details group metadata, parameters, preview and guidance. Localize Any shell in the profile and command metadata.
- Show dispatch eligibility guidance in the details footer, including offline, incompatible profiles, missing parameters and copy-only multiline/secret cases. This replaces stale render warnings in the operation-status notice. Existing render validation, host/connection tickets, execution confirmation and copy-secret confirmation remain in use.

Validation uses the existing `--commands` bounded UI scenario on disposable PL/EN data, extended for the new interactions: each risk level, reset/profile/focus retention, empty custom-command editor cancellation, favorites recovery, explicit history opt-in, immediate Enter/Ctrl+F/Escape and visible offline guidance. Existing card pointer/Enter/details/back/copy/favorites and RDP/Serial isolation checks remain, including compact font13/16 with collapsed/expanded system profiles. The scenario uses an in-memory credential fixture and no real SSH server.

71 filtered `CommandEngineTests` and `LocalizationTests` cover the existing rendering/dispatch rules and catalog parity. Incremental Release build, repository/link checks and whitespace checks apply. Selected final renders/TRX/summary are retained in `artifacts/reports/commands-usability-2026-10-17/`; temporary databases stay outside retained evidence.

No complete application matrix, release packaging, VMware, native Linux/hardware or real remote-command execution is claimed for this increment. No database migration, protocol or plugin-lifecycle change is introduced.
