# Session Summary — 2025-07-12

## Overview
Phase 2 feature work, stability fixes, v1.4.0 → v1.4.1 release, full documentation update.

## Commits This Session
| Hash | Description |
|---|---|
| `d8d290c` | feat: phase 2 multi-fix — drag, dialog owner, hotkey stability, keyboard+mouse separation, settings context menu |
| `3d89aca` | fix: Settings now in group window body context menu |
| `db6dea1` | chore: bump version to 1.4.0 |
| `453ecd7` | docs: update README and in-app help for v1.4.0 features |
| `bc3f21d` | chore: bump to v1.4.1 — in-app help panel updates and About version fix |

## Changes by Area

### Phase 2 — Hotkeys (keyboard + mouse separation)
- `HotkeyMode.cs` — new enum: Keyboard / Mouse / Both
- `AppSettings.cs` — 4 new fields: `ToggleAllMouseHotkey`, `ToggleAllHotkeyMode`, `FullscreenMouseHotkey`, `FullscreenHotkeyMode`
- `HotkeyBox.xaml.cs` — `CaptureMode` DependencyProperty (All / KeyboardOnly / MouseOnly); Deactivated auto-cancel; duplicate hotkey detection
- `SettingsWindow.xaml` — 2-row layout per action: mode ComboBox row + ⌨️ HotkeyBox + 🖱️ HotkeyBox row
- `SettingsViewModel.cs` — `OwnerWindow` property; 4 new observable props; dialog owners set; `ValidateHotkeys` covers all 4 fields; `ResetToDefaults` updated
- `SettingsWindow.xaml.cs` — Win32 WM_NCLBUTTONDOWN drag fix (replaced `DragMove()`)
- `HotkeyWarningDialog.xaml` — `WindowStartupLocation=CenterOwner`
- `MainViewModel.cs` — `RegisterHotkeys()` mode-aware

### ⚙️ Settings in group body context menu
- `GroupWidget.xaml` — `Window.ContextMenu` entry added
- `GroupWidget.xaml.cs` — `MenuSettings_Click` handler; `SettingsRequested` C# event
- `DragThumb.xaml.cs` — `OpenSettingsRequestedEvent` routed event
- `App.xaml.cs` — `SettingsRequested` subscription in `CreateGroupWindow`

### Documentation
- `README.md` — Ctrl+Alt+G → Ctrl+F2, new features table rows, shell context menu note, Quick Start step 6, build markdown fixed
- `SettingsWindow.xaml` Help panel — Settings entry in Right-Click Menu section; Mouse hotkeys in Keyboard Shortcuts section; hotkey mode + shell context menu tips

### v1.4.0 build
- `DeskGroup-Setup-1.4.0.exe` — 69 MB
- `DeskGroup-Portable-1.4.0.zip` — 68.6 MB

### v1.4.1 patch
- `SettingsWindow.xaml` About tab `VersionText` fixed: `1.3.0` → `1.4.1`
- `DeskGroup.csproj` version: `1.4.0` → `1.4.1`
- `DeskGroup-Setup-1.4.1.exe` — 69 MB
- `DeskGroup-Portable-1.4.1.zip` — 68.6 MB

## Known Issues at Close
- None critical
- User off to test v1.4.1; patch list expected next session if issues found

## State at Close
- Branch: `Dev`
- HEAD: `bc3f21d`
- Git: clean (all committed + pushed)
- Build artifacts: `build\output\` contains both 1.4.0 and 1.4.1 sets
