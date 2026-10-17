# Calmer workspace presentation — 2026-10-17

Focused UI iteration after `445c96b`. No application version bump, installer or release tag. Existing preferences, profile/tag storage, clients and plugin lifecycles are unchanged.

## Changes

- Navigation groups use spacing instead of separate filled frames. The active module remains highlighted. Sidebar headers use a small neutral icon and one title; the contextual description remains available as a tooltip.
- Protocol selection and tags share one compact row. The protocol selector keeps all five choices and stays synchronized with the existing filter state and Clear action. An inactive tag picker no longer shows a decorative total; active tag counts and filter chips remain visible.
- Tree tags retain their colors as small dots with neutral text. Active tag matches and editable/removable labels keep stronger treatment. Protocol labels and folder counts lose their individual boxes. Folder glyphs, custom icons, bold names, spacing and empty labels preserve the hierarchy.
- Tree selection paints the actual Fluent `PART_LayoutRoot` with `SurfaceSelected`, rather than the vivid default accent surface. This also keeps secondary labels readable on the selected row. Primary/secondary text resources and keyboard focus cues are retained.
- The welcome page has a shorter introduction, a smaller unboxed brand mark and quiet actions that highlight on hover/focus. Session toolbars retain a subtle plugin separator without the repeated plugin badge.
- Empty metric cells stay hidden before SSH is connected or a snapshot exists. Last measured data remains visible after disconnection. This only changes presentation, not sampling or session behavior.

## Focused verification

- Incremental Release build and eight localization checks.
- `--connection-tags`, PL/EN: compact protocol selection and clear/reset, existing exact any/all/untagged filters, search/protocol intersection, immediate Enter protection, staged colors/cancel/apply, editor flows, temporary folder expansion and long-label bounds. Graphite/Light, 900 × 680 and font 16, compact tag dialogs.
- `--workspace-details`: existing folder icon edit/save/cancel behavior; selected tree background matches the Dark/Light theme surface; compact synthetic metric samples in Midnight/Light; empty metrics hidden and the last measured snapshot retained after disconnect.
- `--session-chrome`: local simulated credential-wait presentation, loading containment, no tab underline, active group/color markers, 900 px split view and 200% rendering. No real server or credential read.
- Selected welcome/tree/session captures visually inspected. Repository content and whitespace checks passed. No full solution, hardware/platform matrix or release packages.

Selected evidence is retained in `artifacts/reports/calm-interface-2026-10-17/`. Disposable harness data is isolated from maintainer profiles and credentials.
