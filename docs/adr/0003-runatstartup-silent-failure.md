# 3. RunAtStartup adapters silently swallow API failures

**Status**: Accepted

Both `MsixRunAtStartup` and `RegistryRunAtStartup` wrap their `Enable()` and `Disable()` operations (and the MSIX `IsEnabled` getter) in `catch { }` blocks that suppress all exceptions. This looks like a bug to a future reader who sees `catch { }` with no logging, but the behaviour is deliberate: `OnToggleStartup` re-queries `IsEnabled` after every toggle and uses the resulting boolean to refresh the menu checkbox state. So if the toggle silently failed, the checkbox state self-corrects on the next read — the UI never lies about persistent state, even though it doesn't surface the failure to the user as a notification.

We considered (and rejected for now) returning `bool` from `Enable`/`Disable` so the caller could balloon-tip on failure. That's a worthwhile behaviour change but separate from the architectural extraction it would have ridden in on. A future reader who wants to add error-surfacing should change the interface to `bool` and wire the menu handler to display a notification on failure — not "fix" the silent swallow as if it were an oversight.
