# Quick Start Guide

## The "Black Dot" Mystery - SOLVED! ✅

**What you were seeing:** DeskGroup WAS running successfully, but the tray icon wasn't visible because:
1. Icon resources were commented out in the project file
2. The app was using a fallback icon that wasn't displaying properly
3. The "black dot" was actually the app starting/stopping or the invisible helper window

## What I Fixed

1. **Re-enabled icon resources** in `DeskGroup.csproj`
2. **Fixed helper window** to be truly invisible (moved off-screen, set opacity 0)
3. **Added diagnostic logging** throughout startup
4. **Added startup message box** showing tray icon status

## How to Run NOW

### Option 1: From Visual Studio (Recommended for first test)
1. **Stop any running instances:**
   ```powershell
   Stop-Process -Name DeskGroup -Force -ErrorAction SilentlyContinue
   ```

2. **Press F5** in Visual Studio

3. **You should see:**
   - Diagnostic message box showing:
     - "Tray icon: Created"
     - "Groups loaded: 0" (or number of existing groups)
     - "First run: True/False"
   - After clicking OK, look for tray icon in notification area

4. **Check Debug Output** (View → Output in VS):
   - Should show detailed startup logs
   - Look for "=== DeskGroup Starting ===" through "=== DeskGroup Startup Complete ==="

### Option 2: Run Directly
```powershell
# Kill any running instances first
Stop-Process -Name DeskGroup -Force -ErrorAction SilentlyContinue

# Run the app
Start-Process "C:\Dev\DeskGroup\src\DeskGroup\bin\Debug\net10.0-windows\DeskGroup.exe"

# Verify it's running
Get-Process DeskGroup

# Check for tray icon in system tray (bottom-right of screen)
```

## What to Look For

### 1. Diagnostic Message Box
When you run the app, you should immediately see a message box with:
```
DeskGroup started successfully!

- Tray icon: Created
- Groups loaded: 0
- First run: True

Look for the DeskGroup icon in your system tray.
Right-click it to create your first group!
```

### 2. System Tray Icon
- Look in notification area (bottom-right of taskbar)
- Click the "^" arrow if icon is hidden
- Icon should be visible (application icon or custom DeskGroup icon)

### 3. Tray Icon Menu (Right-Click)
Should show:
- Show All Groups
- Hide All Groups
- ──────────
- New Group
- ──────────
- Sort Ungrouped Icons...
- ──────────
- Settings...
- ──────────
- Exit DeskGroup

## Creating Your First Group Widget

1. Right-click tray icon → **"New Group"**
2. A widget window appears on your desktop
3. Right-click the widget → **"Add shortcut..."**
4. Drag desktop icons into the widget

## If You Still See Issues

Share with me:
1. Does the diagnostic message box appear? What does it say?
2. Do you see the tray icon after clicking OK?
3. Any errors in Debug Output window?

## Clean Slate (If Needed)

To completely reset and test fresh:
```powershell
# Kill running instance
Stop-Process -Name DeskGroup -Force -ErrorAction SilentlyContinue

# Delete saved data
Remove-Item -Recurse -Force "$env:APPDATA\DeskGroup" -ErrorAction SilentlyContinue

# Rebuild
cd C:\Dev\DeskGroup
dotnet build -c Debug

# Run
Start-Process "src\DeskGroup\bin\Debug\net10.0-windows\DeskGroup.exe"
```

This will give you a completely fresh first-run experience.
