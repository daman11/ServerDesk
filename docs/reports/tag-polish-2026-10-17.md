# Connection tag usability — 2026-10-17

Focused UI iteration after `7e6e7ff`, using the existing tag model and color storage. No database, protocol, credential or release changes.

- Unified rounded labels use subtle color fills, readable theme text, bounded single-line names and an integrated remove action. Connection rows keep short labels together and reserve space for the overflow count.
- The profile editor starts with assigned labels and a searchable add field. Enter/+ supports multiple pasted names and deduplicates assignments; selecting a suggestion clears the query. All known tags can be found, including those beyond the bounded suggestion preview. Escape clears the add field. Save includes a pending entry. Direct list editing is available in a collapsed expander. Cancel preserves stored assignments.
- The filter dialog has a page heading, wider labels, named color choices, selected-only review, additive selection of visible search matches and a draft connection-count preview. The preview uses exact any/all/untagged membership intersected with the current browser query, protocol and Favorites/Recent scope.
- Search has a clear action and Ctrl+F focus. Escape clears the query before closing. Checkbox selection refreshes its label immediately. The sidebar displays the matching mode for multiple selected tags and explains its available/active counters.

## Focused verification

- Updated `--connection-tags` production UI fixture, PL/EN: existing exact filters, immediate Enter protections, staged colors/cancel/apply and folder collapse behavior; new selected-only review, bulk selection, draft any/all preview, Escape search clearing, searching beyond the first 12 suggestions, Enter with multi-tag input, duplicate prevention, rapid chip removal and save with a pending tag.
- Graphite/Light screenshots at 900 × 680 with interface font 16 and compact 500 × 530 tag dialogs; long-label bounds and accessible footer checks. Visual inspection of editor, filters and tree captures.
- Eight localization checks for catalog completeness, placeholders and existing language behavior. Incremental Release build, repository content check and whitespace check.

Evidence: `artifacts/reports/tag-polish-2026-10-17/`. Disposable profiles and credentials remain isolated from maintainer data. No full matrix, installer, version bump or release tag for this UI iteration.
