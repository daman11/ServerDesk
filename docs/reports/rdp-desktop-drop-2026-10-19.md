# Windows RDP: embedded default and direct file drops — 2026-10-19

Development increment after 0.43.0; no application version change or release packages. Work uses the existing Microsoft RDP control, credential/certificate checks and automatic clipboard channel. No production profiles, credentials or user files were used.

## Behavior

New profiles and serialized RDP options without a display mode default to **Embedded**. Explicitly saved External/Embedded values retain their meaning. Windows users can change an existing profile in its editor; Linux already embeds its X11 FreeRDP client.

Windows accepts local files/folders over the native RDP surface as Copy-only OLE `CF_HDROP`. An owned target is registered on the control's HWND and released before destruction. Native drops bypass Avalonia routed events, which is why a handler on the toolbar alone could not support the desktop surface. The toolbar also had a null background: its padding/gaps did not receive pointer/drop events. A transparent hit-test background fixes that separate issue.

Both routes reuse `RdpFileSelection`: 1–256 selected items, at most 10,000 entries including descendants, total below 2 GiB, no symlinks/junctions. Direct drop activates its session, prepares the existing OS file clipboard, targets the owned visible RDP child and sends one remote Ctrl+V through `IMsRdpClientNonScriptable::SendKeys`. There is no global keyboard injection or new drive redirection. The native interface/vtable was checked against the installed `mstscax.dll` type library; the working key payload uses scan codes and a separate key-up array.

The target is revalidated before and after a short cancellable native-event settling interval: same logged-in host, active pane/window, unchanged bounds and clipboard sequence, visible owned child at the point. Preparation does not display the general busy row, which would move the viewport. A fixed-width toolbar zone also prevents status text from shifting the desktop. Disconnection, new clipboard data, moved/obscured viewport or focus change prevents automatic paste. No automatic retry is attempted because an unacknowledged paste may already have created files.

Drop on the visible remote desktop background or an open Explorer folder's file area. Remote Windows handles the copy destination, name conflicts, progress and completion. ServerDesk reports **paste submitted**, not **transfer complete**. The toolbar remains available for explicit preparation followed by manual Ctrl+V. Direct drops on arbitrary remote applications are not a general OLE forwarding protocol; a text editor or application icon can interpret paste differently.

## Actual desktop acceptance

Host: Windows, one 1920×1080 screen at 100%. Guest: authorized disposable Windows 11 Pro VM, local test account, VMware. The final authenticated RDP viewport was 1596×757 at 100%. Certificate verification/NLA remained enabled. Credentials arrived through a current-user-only pipe and were not stored in tracked files or reports. The VMware user clipboard agent was stopped temporarily to distinguish RDP transport from VMware clipboard sharing.

| Check | Result |
|---|---|
| Physical file gesture from ServerDesk's synthetic drag source onto the remote white receiver | User confirmed accepted; receiver recorded the 74-byte file and matching SHA-256 |
| Eight successive direct drops of one file and a folder through the product path | 8/8; every file and nested file matched bytes and SHA-256 |
| Unicode text and PNG, local → remote and remote → local before drops | 4/4; destination replaced with a different sentinel before each case |
| Same text/PNG checks after the eight drops | 4/4, including exact pixel comparisons |

The final bounded matrix contains **16 passing cases**. Calls through the fixed fixture controller exercise the product's paste/transfer path; they do not count as physical drag gestures. The physical file gesture was independently confirmed by the user and receiver. The receiver accepts only its fixed synthetic filenames/content and writes fresh destinations inside its own test root.

Synthetic file hashes:

- 74-byte Unicode text file: `48A23C1E4EE65D17C32B980C348109917D5660E281A3F57CA1C160D8AE84C2BB`.
- 18-byte nested file: `D081DCEDD37D8DBDF716670D38B6489B3C9FC9327F0BD0B40AA0AB9851BF8B08`.

The first repeated run submitted paste immediately after a native click: four drops passed and the fifth produced no receiver paste entry. That failed attempt remains separate evidence. Allowing the native mouse/clipboard queue a short settling interval preceded the final 8/8 run; this is a bounded scheduling safeguard, not a remote acknowledgement or proof against every latency/policy.

An experimental manual-clipboard synchronization approach transferred files but regressed normal text/image copying. It was removed. The final implementation never changes `ManualClipboardSyncEnabled`, retains the original RDP control CLSID and passes ordinary clipboard checks before/after file transfers.

## Focused regression checks

- **49/49 xUnit**: RDP defaults, explicit-mode persistence, identities/security options, file-selection bounds/cancellation and repository options.
- **Native OLE**: Unicode paths/coordinates, Copy-only, cancellation, state change, replay, empty/oversized/relative/unreadable payloads, callback/presentation failures, repeated disposal and re-registration.
- **Native host**: control creation, retained HWND across delayed detach/reattach and repeated disposal releasing the HWND.
- **Headless drop surface**: hit testing in padding at 240/520/1100 px; unavailable/unsupported data returns None.
- **Workbench controller**: malformed/oversized JSON and IDs, reentrancy, duplicate request containment, UI-thread continuation and safe error responses.
- **RDP pane**: explicit external-client state, preflight/start/stop/reconnect, shutdown drain, profile-editor persistence, native-host failure recovery and narrow layout. Updated an old test lookup to the current fullscreen label.
- **Help PL/EN**: 14 topics, searching, navigation, keyboard behavior and layout, including the revised RDP instructions.

Builds and tests are targeted; the full release/platform matrix was not run. Selected ignored evidence is in `artifacts/reports/rdp-direct-drop-2026-10-19/`: `final-matrix.json`, `receiver-final.json`, `interaction-final.json`, `matrix-before-settling.json`, `cleanup.txt` and `cleanup-verification.txt`. Unit results are in `artifacts/rdp-direct-drop/tests/rdp-final.trx`.

## Cleanup and remaining acceptance

Closed owned workbenches/fixtures and the bounded fixture download server. Restored the VM's original RDP deny setting and stopped TermService, removed the exact owned firewall rule and scheduled tasks, restored the original Polish language/input list and restarted its VMware user agent. The installed Japanese language capability remains a test prerequisite. Synthetic fixture files remain under the owned acceptance directory; no unrelated files were removed.

This does **not** close physical AltGr/Japanese IME through ServerDesk: the user's earlier successful typing was in the VMware console. It also does not establish every remote clipboard format (notably HTML), physical mixed DPI, every account/policy, reverse physical drag, long sessions, large/interrupted/conflicting Explorer copies or all Explorer destinations. Linux direct desktop drops are not implemented. See the [full matrix](../rdp-test-matrix.md) and [user behavior/limits](../rdp-integration.md).

References: [OLE registration](https://learn.microsoft.com/en-us/windows/win32/api/ole2/nf-ole2-registerdragdrop), [RDP SendKeys interface](https://learn.microsoft.com/en-us/windows/win32/termserv/imsrdpclientnonscriptable-sendkeys), [clipboard redirection flow](https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-rdpeclip/9a784d09-bf53-4362-995d-a20d0eb5f074).
