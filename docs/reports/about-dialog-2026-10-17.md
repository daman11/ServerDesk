# About dialog — 2026-10-17

Focused UI increment after 0.41.0; no release or application version change.

`Menu → About` now opens a resizable dialog with App, License and Components tabs. Product/build metadata comes from the application assembly; technical information includes OS, process architecture and runtime/Avalonia versions. An explicit copy action reports success or clipboard failure and copies no saved profiles, credentials, paths or logs. Technical text remains selectable.

The MIT text is embedded directly from the repository's `LICENSE` file and available offline. It is displayed verbatim in English alongside a PL/EN summary. The components tab lists key projects and points to the existing complete notices/inventory in distribution packages; it does not relabel their assets or dependencies as MIT. No upstream license or pinned asset is modified. The obsolete About sentence describing all Linux RDP as a separate window is removed.

Validation: incremental Release build, 8 filtered `LocalizationTests`, and isolated `--about` UI checks on PL/EN, Graphite/Light, font16 and 680×640 / 460×480 layouts. Check all tabs, exact MIT text, scrolling to the license end, clipboard text/feedback, footer/tab bounds and Escape. No profile repository or network connection is used. Selected renders and localization TRX are retained in `artifacts/reports/about-dialog-2026-10-17/`.

Repository/link and whitespace checks apply. No full application matrix, packaging, native Linux or hardware acceptance is claimed.
