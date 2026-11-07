# Windows RDP viewport fitting — 2026-11-07

Focused display fix after 0.51.0. The maintainer clarified that only the RDP
image exceeded the available area after moving to a smaller monitor; the
ServerDesk window itself fitted. Main-window placement is unchanged.

## Finding and change

The previous path set SmartSizing when connecting and sent a remote display
request after size/DPI changes. It did not explicitly resynchronize the ActiveX
in-place object's pixel rectangle or reapply local fitting after a display
transition. A stale local extent/fit state could therefore remain independently
of the requested remote resolution.

- After the existing layout debounce, synchronize the ActiveX object/clip
  rectangle with the actual native host client rectangle. The remote display
  request uses these client pixels and the current top-level scale.
- Reapply SmartSizing when fitting is enabled, including when a remote display
  update fails. An explicitly disabled profile setting stays disabled.
- Monitor moves, work-area/scale changes, restore and reparenting invalidate
  display deduplication even when final pixel dimensions happen to match.
- A remote-size notification resynchronizes local fitting without invalidating
  the last server request, avoiding a loop of identical display requests.
- Skip detached, hidden, minimized, zero-size and disposed hosts. Remove old
  parent subscriptions on detach/dispose. A viewport COM failure uses the safe
  logger, with repeated identical HRESULTs suppressed until recovery.

Microsoft documents [SmartSizing](https://learn.microsoft.com/en-us/windows/win32/termserv/imsrdpclientadvancedsettings-smartsizing)
as local fitting to the control's client area, independently writable while
connected. [SetObjectRects](https://learn.microsoft.com/en-us/windows/win32/api/oleidl/nf-oleidl-ioleinplaceobject-setobjectrects)
uses pixel rectangles for the in-place object and its visible area. These are
separate from the remote session's display-resolution request.

## Focused verification

- **12/12 RdpDisplayTests pass**, covering physical dimensions, scale factors,
  equal pixels with different DPI, odd widths and invalid viewports.
- **Native Windows ActiveX fixture PASS**, without calling Connect or using
  credentials. The installed control reports version 10.0.26100.
- Actual available monitors: work areas 2560 × 1032 at `(0,0)` and
  1920 × 1032 at `(-1920,0)`, both 100%. Moving the disposable native window
  between them changed the host/object from **2400 × 900** to **1824 × 900**.
  The in-place object stayed inside its host, and the move invalidated stale
  display settings despite unchanged DPI.
- Synthetic WM_DPICHANGED messages target only the disposable HWND, exercising
  100→150→200→100% without modifying system display settings. Client/object
  bounds match the Avalonia viewport in physical pixels; display scale factors
  follow the current parent.
- A deliberately oversized in-place extent and disabled fit setting recover.
  Shrink, hide/show, minimize/restore and detach/reattach retain the same HWND.
  Old-parent DPI events stop affecting it; new-parent events invalidate the
  display cache. An explicit fit opt-out remains honored.
- A simulated login callback permits exercising a display update on the
  disconnected control. The API rejects it with HRESULT **0x8000FFFF**; local
  fitting remains enabled. This is an offline rejection, not a live server test.
- Existing native repaint/timer/retained-HWND and repeated-disposal checks pass.
  The viewport fixture also confirms disposal destroys the owned HWND and
  rejects subsequent synchronization.
- Harness and incremental Windows development builds pass without warnings or
  errors; repository/link and diff checks pass.

Evidence: `artifacts/reports/rdp-viewport-2026-11-07/rdp-display.trx` and
`native-monitors/native-viewport.json` (14 checkpoints, PhysicalMonitorMove=true,
LiveRdp=false, SyntheticDpi=true). `--native-rdp-viewport` is the opt-in fixture.
The earlier native attempt exposed a missing detached-root guard; it failed and
is retained separately, not counted as PASS.

## Limits

The fixture uses the real control and native windows, but no authenticated RDP
bitmap. It validates geometry, fit state, callbacks and cleanup. The reported
symptom still needs confirmation by moving the maintainer's active RDP session
with the development build. Physical mixed-DPI monitors, remote coordinate/input
mapping and live server acceptance remain open in the [RDP matrix](../rdp-test-matrix.md).

No connection, authentication, certificate, clipboard or stored-profile policy
was changed. No VM, complete release matrix, version bump or release package.
