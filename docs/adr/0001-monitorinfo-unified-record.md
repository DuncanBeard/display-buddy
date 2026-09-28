# 1. MonitorInfo as the canonical display record, with the cheap polling path preserved

**Status**: Accepted

`DisplayMonitor` (the change-watcher) and `MonitorInfoProvider` (the all-monitors snapshotter) previously each computed their own DPI scaling and exposed their own record types (`DisplayInfo` vs `MonitorDisplayInfo`). The DPI math was duplicated character-for-character, and the records overlapped in five fields. We unified on a single `MonitorInfo` record with a pure static `Compute(nativeW, nativeH, dpiX, dpiY)` factory that both producers now call.

We deliberately kept `DisplayMonitor` on the cheap `Screen.PrimaryScreen` + `GetDpiForMonitor` path rather than routing it through `MonitorInfoProvider.GetAllMonitors()`. The CCD enumeration in `GetAllMonitors` does several `DisplayConfigGetDeviceInfo` round-trips per connected monitor; running it on every 60-second poll tick (and on every `WM_DISPLAYCHANGE`) would be wasteful when the watcher only needs the primary's resolution. Rich fields (FriendlyName, color depth, HDR, refresh rate, VRR) default to sentinel values when sourced from the cheap path; full enrichment happens only on menu-open via `MonitorInfoProvider`.

A future reader who asks "why aren't both producers sharing more code?" should read this ADR before unifying the data sources — the duplication that remains is intentional cost-shifting, not an oversight.
