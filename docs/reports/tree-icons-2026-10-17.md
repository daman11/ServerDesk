# Folder presentation and service icons — 2026-10-17

Small UI development step after 0.40.0; no version change or release package.

- Folder headers now use a separate surface, bold names, consistent expander slots and an explicit folder symbol. A custom service/role icon appears alongside it. Empty leaves show localized `Pusty / Empty`; parents containing only subfolders show 0 servers instead. Open/closed folder glyphs follow expansion without changing saved tree state.
- Profile/folder catalogs gain 36 system/service symbols: 35 pinned Simple Icons silhouettes plus an application-owned Windows Server symbol. There are 51 profile choices, including automatic/role icons. Filled marks inherit theme foreground; existing Lucide assets retain their original stroke rendering and bytes. Custom imported graphics still take precedence.
- Sources, asset hashes, attribution and applicable license texts are retained under `third-party/simple-icons/`. The notice generator includes unchanged SVGs and notices in future packages; repository/release guards verify their hashes. [Catalog and source information](../icons.md).

Focused Windows checks:

- Release UI harness build: zero errors/warnings.
- `--icons`: pinned SVG hashes and 24×24 bounds, all catalog resolutions, 16/24 px galleries in Dark/Light, inherited foreground, profile editor save with Proxmox, existing live-icon/disposal checks, and folder-header/picker checks in PL/EN. Folder cases include branded empty leaves, zero-server parents, open/closed glyphs, equal expander widths, selection, 220/280 px sidebars at font 16, PostgreSQL save and Grafana cancel.
- 16 filtered `ProfileIconTests`/`FolderIconTests`: existing legacy/future-ID behavior plus persisted/duplicated/exported/imported Docker, Proxmox and Windows Server choices.
- Existing `--browser-workflow`: selection, rename/delete, drag/drop and keyboard regression after changing headers.
- Notice generator exercised twice against a temporary payload metadata fixture: all 35 SVGs, attribution, upstream notices and license texts present with matching hashes; no nested icon directories on regeneration.
- Repository content/link/hash check and staged whitespace check.

Evidence: `artifacts/reports/tree-icons-2026-10-17/`. Test databases are disposable and no real server or personal credential store is used. These are headless Windows layout/input checks, not native Linux, physical gestures, mixed-DPI or full release acceptance. A development Windows executable is prepared from the final local commit.
