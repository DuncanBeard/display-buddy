# 2. RunAtStartup is a public interface with sibling adapters, not a single class

**Status**: Accepted

The "launch on user login" feature has two real adapters today: `Windows.ApplicationModel.StartupTask` for MSIX-packaged builds and `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` for portable builds. We extracted these from `TrayApplicationContext` into a public `IRunAtStartup` interface plus two sibling adapter classes (`MsixRunAtStartup`, `RegistryRunAtStartup`), with a static `RunAtStartup.Create()` factory that picks the right one based on a one-time `Package.Current` probe.

A simpler alternative was a single `RunAtStartup` class with private internal adapters. Given the project's "Pragmatic Code" principle (see `.specify/memory/constitution.md` III), that's where initial instinct points. We deliberately chose the more ceremonial shape because (a) the architecture-review rule "one adapter = hypothetical seam, two = real seam" actually applies — both adapters exist in production today, (b) a third adapter is plausible (Store sideload, future Windows mechanism), and (c) the public seam keeps `TrayApplicationContext` from ever needing to know about packaging again.

Future readers wondering why we kept the interface for a single-process WinForms app with no DI container should not collapse it to a static class without first considering whether a third adapter is on the horizon.
