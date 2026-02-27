# DeskGroup Startup Diagnostic Guide

## Expected Behavior

DeskGroup is a **system tray application** with no main window. When you run it:

### First Run (no existing groups):
1. ✅ **System tray icon** appears (look in notification area, bottom-right of taskbar)
2. ✅ **Balloon tip** appears: "DeskGroup is running - Right-click the tray icon..."
3. ❌ **No windows** appear (normal - no groups created yet)

### Subsequent Runs (with existing groups):
1. ✅ **System tray icon** appears
2. ✅ **Group widget windows** appear on desktop (one for each saved group)
3. ❌ **No main window** (by design)

## How to Verify App is Running

### Check 1: System Tray Icon
Look in your system tray (notification area) for the DeskGroup icon. You may need to:
- Click the "Show hidden icons" arrow (^) in the taskbar
- Right-click the tray icon to see the context menu

### Check 2: Process List
Run in PowerShell:
```powershell
Get-Process | Where-Object {$_.ProcessName -like '*DeskGroup*'}
```

### Check 3: Debug Output
Run the app from Visual Studio (F5) and check the **Debug Output** window for these logs:
```
=== DeskGroup Starting ===
Single instance acquired
Initializing MainViewModel...
MainViewModel initialized. Groups: 0
Creating system tray icon...
Tray icon created: True
Creating 0 group windows...
=== DeskGroup Startup Complete ===
```

## Troubleshooting

### Issue: No tray icon visible
**Possible causes:**
1. Icon resource missing or failed to load
2. Hardcodet.Wpf.TaskbarNotification package issue
3. Exception during `CreateTrayIcon()`

**Fix steps:**
1. Check Debug Output for exceptions
2. Check that `Assets/tray-icon.ico` exists in project
3. Try fallback: app should use `SystemIcons.Application` if custom icon fails

### Issue: App crashes silently
**Possible causes:**
1. Unhandled exception during `InitializeAsync()`
2. File I/O error when loading settings/groups

**Fix steps:**
1. Run from Visual Studio with debugger (F5) - exceptions will be caught
2. Check Debug Output for "Initialization failed:" messages
3. Check that user has write access to `%APPDATA%\DeskGroup`

### Issue: Groups exist but windows don't appear
**Possible causes:**
1. Windows are positioned off-screen (multi-monitor setup changed)
2. `IsVisible` property is false
3. Exception during window creation

**Fix steps:**
1. Check Debug Output for "Creating N group windows" and individual window logs
2. Right-click tray icon → "Show All Groups"
3. Delete `%APPDATA%\DeskGroup\groups.json` to reset

## Creating Your First Group

Once the tray icon is visible:
1. Right-click the tray icon
2. Select "New Group"
3. A group widget window should appear on your desktop
4. Right-click the widget → "Add shortcut..." to add desktop icons

## Testing the Fix

Run this in PowerShell after starting the app:
```powershell
# Check if running
Get-Process DeskGroup -ErrorAction SilentlyContinue

# Check tray icon (requires manual visual check)
# Look in system tray notification area

# Check data directory
explorer $env:APPDATA\DeskGroup
```

## Next Steps

If you still see no output after following these steps, please share:
1. Debug Output logs (from Visual Studio Output window)
2. Any error messages or exceptions
3. Result of `Get-Process DeskGroup`
