# Bug: TaskbarAlignmentTool poisons shell foreground activation on boot

**Date:** 2026-05-27  
**Severity:** High — makes taskbar completely non-functional  
**Status:** Needs RCA and fix

## Symptoms

- Taskbar clicks on app icons do not switch focus to the target app
- Clicked apps flash orange in the taskbar instead of activating
- Alt-Tab works (bypasses taskbar entirely)
- Task Manager (elevated/UIAccess) can still be launched from taskbar
- Restarting explorer.exe does not fix the issue
- Killing TaskbarAlignmentTool does not fix the issue (damage already done)
- Only a full sign-out/sign-in restores normal behavior

## Root Cause (Hypothesis)

TaskbarAlignmentTool runs at logon via `HKCU\...\Run` and hooks into the shell (Shell_TrayWnd). During startup, it appears to interact with the taskbar in a way that causes **explorer.exe to lose its foreground activation token**.

In Windows, a process can only call `SetForegroundWindow()` successfully if it received the last user input event (or was granted the right). The Win11 XAML taskbar's button-click handler runs on a different thread than the one receiving mouse input. If the foreground activation chain is disrupted during shell initialization, the taskbar permanently loses the ability to activate other windows.

### Contributing factors observed:

1. **DPI mismatch on boot** — System DPI reported 96 (100%) while taskbar rendered at 144 (150%). The tool may be interacting with the shell before DPI negotiation completes.
2. **Elevated AMD driver tasks** (`AMDInstallUEP`, `AMDScoSupportTypeUpdate`) run at logon and may steal the foreground token before the tool finishes its shell hook setup.
3. **Boot timing sensitivity** — Issue appeared after a BIOS VRAM change (512MB → 16GB) which altered GPU/display initialization timing.

## Diagnostic Evidence

```
SetForegroundWindow(Outlook) = False  // from elevated PowerShell
BringWindowToTop(Outlook) = True      // Z-order works
AllowSetForegroundWindow(ASFW_ANY) = True  // permission granted
SetForegroundWindow(Outlook) = False  // STILL fails

// Only the keybd_event(VK_MENU) trick + SetForegroundWindow works
// Proves the session's foreground activation chain is broken
```

## Temporary Mitigation

- Removed from `HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run`
- Disabled `AMDInstallUEP` and `AMDScoSupportTypeUpdate` scheduled tasks
- User must sign out/in to restore session

## RCA Tasks

- [ ] Determine exactly when/how the tool interacts with Shell_TrayWnd during startup
- [ ] Check if the tool calls any Win32 APIs that consume or lock the foreground token (e.g., `SetForegroundWindow`, `LockSetForegroundWindow`, creating topmost windows)
- [ ] Test with delayed startup (e.g., 10-second sleep before hooking) to see if timing is the issue
- [ ] Investigate whether the tool needs `UIAccess=true` in its manifest to safely interact with the shell
- [ ] Reproduce with/without elevated logon tasks to isolate interaction effects

## Fix Ideas

1. **Delay hook registration** — Wait until the shell is fully initialized (detect via `Shell_TrayWnd` existing AND responsive) before hooking
2. **Avoid foreground-stealing APIs** — Audit all Win32 calls; ensure no transient windows or `SetForegroundWindow` calls during init
3. **Use UIAccess manifest** — If the tool needs shell interaction, sign it and add `UIAccess="true"` to bypass foreground restrictions
4. **Register as shell extension** — Instead of hooking from a separate process, integrate as a proper shell extension that runs in explorer's context
5. **Add a health check** — After init, verify the shell can still activate windows; if not, release any locks and retry
