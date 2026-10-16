# Terminal scrollback search — 2026-10-16

Development work after the frozen 0.31.1 release. No dependency or release-package changes.

## Behavior

SSH and Serial panes now have a search button and Ctrl+Shift+F. The inline bar searches literal text in the retained terminal buffer, with match-case and whole-word options. Enter/Shift+Enter in the query and F3/Shift+F3 while search is open navigate and wrap through matches. Escape inside the bar closes it, clears highlights and returns keyboard focus. A separate action returns to current output. Plain Ctrl+F remains remote.

The renderer's physical highlight fragments are grouped by public MatchId for logical counts/navigation, so a match spanning soft-wrapped rows is visited once. Searching uses public Iciclecreek/XTerm.NET APIs on the dispatcher, with a 150 ms typing delay, a 256-character query limit and the library's 10,000-highlight-run result cap. A plus sign indicates a truncated result set; retained logical matches can be fewer than that run cap. No regex engine or private reflection is used in production.

New output marks results stale without repeatedly scanning or moving the viewport. Refresh or the next navigation recomputes results; resizing also schedules an updated search. The status row and search row use a common vertical container above the terminal, with no overlay on its content. Searches pause when hidden, resume pending work, and clear on a successful new connection or pane close. Query state is per pane and is not stored, exported or logged. Closing the bar keeps its field value only within that pane until reset/disposal.

## Verification

- Release solution build: passed, zero warnings/errors. Unit suite: **503 passed, 0 failed, 0 skipped**.
- Windows 11 and Ubuntu/WSL Linux x64 headless UI: SSH and Serial controls in Polish/English, Graphite/Light, font16 and 280/520/1000 px widths. Checked Unicode/emoji, literal regex-like text, case/word options, scrollback navigation/wrap, one logical hit across wrapped rows, empty/no-match results, field/result caps and discarded history.
- Pending hidden search resumes, SFTP hides the bar, tail/Escape/focus work, close clears query/highlights. Geometry checks keep the busy status, search controls and terminal in separate bounds. Representative Windows renders were visually inspected.
- Actual owned OpenSSH on loopback 22222, from Windows and Linux: remote UTF-8 output is searchable; synthetic local search shortcuts including Kitty key-up reporting send no input in the fixture; plain Ctrl+F still sends its remote control character; actual new output marks results stale and refresh includes it; reconnect clears search state. The server was stopped afterwards.
- Existing failed-session lifecycle/GC, session chrome and VNC/Serial protocol acceptance passed on Windows. Eight closed failed panes and linked lifetimes were collected; existing loopback VNC/docking/loss tests passed.
- The offline search scenario is included in Windows/Linux CI. No physical serial adapter or physical keyboard matrix is claimed by these synthetic checks.

Measured explicit refresh over the bounded, capped test buffer took approximately 7–17 ms on Windows and 4–6 ms on Linux/WSL. These local timings are observations, not a general responsiveness guarantee. Timing JSON and rendered controls are under `artifacts/reports/mvp-terminal-search/`.

## Limits

Only retained buffer contents are searched, not discarded history or data on the server. New output requires refresh/navigation for current results. Multi-codepoint combining/ZWJ cluster searches retain the upstream leading-codepoint limitations; tested emoji are single codepoints. Reader/IME/accessibility combinations, physical Serial hardware and other operating systems still need broader acceptance. The application does not persist a searchable terminal transcript.

The analysis of fragment counts and buffer ownership used the [exact XTerm.NET source commit from the installed package](https://github.com/tomlm/XTerm.NET/blob/64a882782667340f353c115be3e7891456efc13c/src/XTerm.NET/Search/BufferSearch.cs).
