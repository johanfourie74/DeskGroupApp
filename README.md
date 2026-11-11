# DeskGroup - Desktop Icon Organizer

**DeskGroup** is a lightweight Windows 10/11 desktop utility that lets you organize your desktop shortcuts into clean, widget-like group containers - similar to Windows 7 gadgets.

Clean up your cluttered desktop while keeping all your shortcuts instantly accessible.

---

## Features

### Four View Modes
| Mode | Description |
|------|-------------|
| **Minimized** | Small pill/badge showing group icon + name. Default resting state. |
| **Hover** | Expands on mouse-over to show your **top 10 most-used** shortcuts. |
| **Normal** | Resizable, draggable window showing **all** shortcuts in the group. |
| **Fullscreen** | Full-screen overlay with large icon grid. Toggle with F11. |

### Smart Organization
- **Drag & drop** shortcuts from Explorer or your desktop directly into groups
- **Right-click -> Add** to browse for shortcuts and applications
- **Auto-detect** new ungrouped desktop icons with gentle reminders
- **Usage tracking** - hover mode automatically surfaces your most-launched shortcuts

### Desktop Integration
- **Sticky mode** - widgets sit on the desktop layer behind all windows (like Win7 gadgets)
- **Float mode** - widgets stay above all windows (like sticky notes)
- **Snap-to-grid** with configurable grid size
- **Pin position** to prevent accidental dragging
- **Multi-monitor support** with per-monitor DPI awareness
- **Optional Side Panel (Edge Dock)** — always-on-top edge dock panel (Left edge by default, hidden until hover/click) that shows/hides independently even when all desktop groups are hidden; features center-aligned icons with space-saving delayed hover tooltips (delay to show, immediate hide on mouse-out), supports pinning, docking to Left/Top/Right/Bottom edges, 3 multi-monitor modes (`MainMonitorOnly`, `SameForAllMonitors`, `PerMonitorCustom` with 1–4 edges per screen), Lock modes (`AutoHide`, `LockedOpen`, `LockedClosed`), and an always-on-top 16×16px (customizable 12–48px) Edge Bubble button

### Customization
- **Per-group accent colors** for easy visual identification
- **Custom group icons** - use any `.ico`, `.exe`, or `.dll` icon
- **Light / Dark / System** theme modes (follows Windows setting)
- **Configurable icon sizes** - follow system default or set your own
- **Configurable hotkeys** — separate keyboard and mouse hotkeys per action; use keyboard only, mouse only, or both simultaneously
- **Shell context menu** — optional "Add to DeskGroup" entry when right-clicking `.lnk`/`.url` files (enable in Settings; on Windows 11 appears in "Show more options")

### Safety & Persistence
- Desktop icons are **safely hidden** (moved to managed storage), not deleted
- **Full restore** - ungrouping or deleting a group restores icons to their original position
- **Auto-save** with debounce (no manual saving needed)
- **Automatic backups** - last 5 snapshots of your group data
- **Atomic file writes** - no data corruption from crashes
- **Portable mode** support - run from a USB drive with no installation

---

## System Requirements

- **OS:** Windows 10 (1903+) or Windows 11
- **Runtime:** .NET 10 (included in self-contained builds)
- **RAM:** ~30 MB
- **Disk:** ~60 MB (self-contained) / ~5 MB (framework-dependent)

---

## Installation

### Option A: Installer (Recommended)
1. Download `DeskGroup-Setup-x.x.x.exe` from the [Releases](#) page
2. Run the installer - no admin rights required
3. DeskGroup starts automatically and appears in your system tray

### Option B: Portable
1. Download `DeskGroup-Portable-x.x.x.zip` from the [Releases](#) page
2. Extract anywhere (USB drive, Documents, etc.)
3. Run `DeskGroup.exe`
4. Data is stored in `DeskGroupData/` next to the executable

---

## Quick Start

1. **Right-click** the system tray icon > **New Group**
2. **Name** your group (e.g., "Browsers", "Games", "Work")
3. **Drag & drop** desktop shortcuts onto the group widget
4. **Hover** over the minimized pill to see your top shortcuts
5. **Double-click** any icon to launch it
6. **Settings** — right-click any group widget → **⚙️ Settings**, or tray icon → Settings
7. **Repeat** - create as many groups as you need!

### Keyboard Shortcuts
| Shortcut | Action |
|----------|--------|
| `Ctrl+F2` | Toggle all groups visible/hidden (default; configurable) |
| `F11` | Toggle fullscreen on active group |
| `Escape` | Exit fullscreen |

*Hotkeys are fully configurable in Settings — choose keyboard, mouse, or both.*

---

## Settings

Access via **tray icon > Settings** or right-click any group widget.

| Setting | Default | Description |
|---------|---------|-------------|
| Theme | System | Light, Dark, or follow Windows |
| Dock mode | Sticky | Sticky (desktop layer) or Float (above windows) |
| Snap to grid | On | Snap widget positions to a grid |
| Grid size | 20px | Grid spacing in pixels |
| Hide grouped icons | On | Move original desktop icons to managed storage |
| Icon size | System | Follow Windows or set custom size |
| Single-click to launch | Off | Launch shortcuts with a single click |
| Start with Windows | On | Auto-launch at login |
| Detect new icons | On | Watch desktop for new ungrouped shortcuts |
| Multi-monitor | On | Allow groups on all connected displays |
| Show/Hide all — Use | Keyboard | Keyboard, Mouse, or Both |
| Show/Hide all — keyboard hotkey | Ctrl+F2 | Keyboard combo to toggle all groups |
| Show/Hide all — mouse hotkey | (none) | Mouse button/scroll to toggle all groups |
| Shell context menu | Off | "Add to DeskGroup" on .lnk/.url right-click |
| Enable Side Panel (Edge Dock) | Off | Optional always-on-top edge dock with independent visibility |
| Edge Dock multi-monitor mode | MainMonitorOnly | MainMonitorOnly, SameForAllMonitors, or PerMonitorCustom (1–4 edges) |
| Edge Dock lock mode | AutoHide | AutoHide (show on hover), LockedOpen, or LockedClosed |
| Edge Dock bubble button | On (16×16px) | Always-on-top edge toggle bubble (customizable 12–48px) |

---

## Changelog

### v1.4.6
- ✅ Fixed: **Edge Dock Power-Failure & Multi-Monitor Wake Resilience** — Added self-healing Edge Dock consolidation (`ConsolidateEdgeDocksForPrimaryMonitor`) and live `SystemEvents.DisplaySettingsChanged` + primary-monitor wake tracking so all primary-monitor Edge Docks (`Left`, `Top`, `Right`, `Bottom`) migrate together as a single unit when monitors power on in staggered order after an AC power outage; automatically deduplicates any same-edge configs so populated panels always win over empty duplicates; added rolling timestamped backups and write-through disk flushing for `settings.json`
- ✅ Added: **Centered Edge Dock Icons, Delayed Hover Tooltips & Close-on-Icon-Click** — Edge Dock icons are strictly center-aligned inside compact square tiles with shortcut labels moved to a 450ms delayed hover tooltip (hiding immediately on mouse-out), and both Edge Docks and temporarily floated groups close/hide on icon click even when Auto-Hide is off
- ✅ Fixed: **Windows 11 Mouse Shortcuts & Hotkey Recording** — Setting a mouse shortcut in Settings or First-Run Setup now automatically upgrades `HotkeyMode` from `Keyboard` to `Both` (or `Mouse`) and registers global `FullscreenToggleHotkey` / `FullscreenMouseHotkey` hooks; `HotkeyBox` now installs a temporary `WH_MOUSE_LL` low-level hook during recording so `MiddleClick`, `XButton1/2`, `ScrollUp/Down`, and `Ctrl/Alt/Shift + LeftClick/RightClick` are reliably captured on Windows 11 without being swallowed by `ScrollViewer` or window chrome; `GetCurrentKeyModifiers()` now uses `GetAsyncKeyState` so background mouse hooks accurately detect `Ctrl`/`Alt`/`Shift`; added a `✕` button to clear any shortcut
- ✅ Fixed: **Windows 11 Right-Click Menus, Sticky Mode & Settings** — Replaced legacy `WorkerW` `SetParent` reparenting in Sticky mode (which placed widgets behind `SHELLDLL_DefView` on Windows 11 23H2/24H2 and broke right-clicks and modal dialogs) with native top-level `HWND_BOTTOM` Z-order enforcement in `WM_WINDOWPOSCHANGING`; activated Sticky widgets on right-click (`PreviewMouseRightButtonDown`) and wired explicit `Click` handlers for `Pin position`, `Snap to grid`, and `Toggle Sticky/Float Mode` so popup `ContextMenu` items never fail silently; fixed `PinToStartMenu` on Windows 11 by creating `%AppData%\Microsoft\Windows\Start Menu\Programs\DeskGroup.lnk`; extended `"Add to DeskGroup"` shell context menu to `.exe` and folders with multi-instance `NamedPipeServer`; wired live updates for all Settings checkboxes and hotkeys
- ✅ Fixed: **Startup & Loading Performance Optimization (Win10 & Win11)** — Added in-memory + persistent `%AppData%\DeskGroup\IconCache` PNG caching and non-blocking background icon extraction (`ShortcutItemViewModel` + `IconExtractorService`); lazily bound `GroupWidget` `ItemsSource` only to the active view mode (`Minimized`, `Hover`, `Compact`, `Normal`, `Grid`, `Fullscreen`) eliminating 4× duplicate `IconTile` instantiation at startup; cached default group icon and replaced 260,000 `Bitmap.GetPixel` calls in `CreateTrayIcon()` with fast `Bitmap.LockBits`; offloaded `CleanupOrphans` disk scan to a background thread
- ✅ Fixed: Tray icon first-click now correctly shows/floats Sticky groups rather than hiding them — toggle direction is based on actual visibility state, not a stale flag (Bug C)
- ✅ Fixed: File dialogs (Add Shortcut, Change Icon) now open centred over their parent window instead of appearing behind it (Bug H)
- ✅ Fixed: Desktop icons now respect the **Hide Grouped Icons** setting — icons are only moved to managed storage when the setting is enabled; when disabled the original `.lnk` remains on the desktop (Bug I)
- ✅ Fixed: Desktop view now refreshes reliably on Windows 11 after an icon is hidden or restored — added `SHCNE_ASSOCCHANGED` broadcast alongside `SHCNE_UPDATEDIR` (Bug I)
- ✅ Added: **Jump List** on taskbar and desktop shortcut right-click — quick access to New Group, Show/Hide All Groups, Settings, and Exit (Note A)
- ✅ Added: **Optional Side Panel (Edge Dock)** — hidden by default on the left screen edge, shows/hides independently even when all other groups are hidden; defaults to 1-icon column width with automatic column-width snapping (`1, 2, 3...` icons wide), hidden vertical scrollbar, 2-row compact header when 1-icon wide (Pin/Lock/Close on Row 1, Title on Row 2), per-dock Icon Size & Opacity slider dialog, independently movable always-on-top 16×16px Edge Bubble button, and keyboard/mouse hotkeys including directional `<Modifier>+Left/Up/Right/Down` arrow keys
- ✅ Fixed: **Zero-jitter moving & resizing** across all Group panels and Edge Docks — replaced dual WPF `Left`/`Top` property updates during drag with single atomic `Win32Interop.SetWindowPos` calls, broke the `OnSizeChanged` → `ViewModel` → `Window` resize feedback loop, and debounced `GridViewWidth`/`GridViewHeight` & Edge Dock drag saves
- ✅ Fixed: **Live updates in Settings** — toggling or changing options in Settings immediately updates the live UI/panels before clicking Apply (and cleanly reverts if discarded)
- ✅ Fixed: **Prevented accidental click-through at 0% transparency** across all Groups and Edge Panels by clamping minimum hit-test alpha to `1/255` (`#01xxxxxx`)
- ✅ Fixed: Multi-monitor fullscreen mode (`F11`) now opens on the group's current monitor instead of always jumping to primary screen `(0, 0)`, and properly restores negative-X secondary monitor positions on exit
- ✅ Fixed: **Start Minimized to Tray** setting is now honored on application startup, and First-Run Setup Wizard selections immediately apply theme/opacity/watcher changes
- ✅ Fixed: Toggling **Detect new desktop icons** in Settings now starts/stops the desktop icon watcher without requiring an app restart
- ✅ Fixed: Uninstaller cleanup now restores hidden icons from both groups and Edge Docks and cleans up both `%APPDATA%\DeskGroup` and `%LOCALAPPDATA%\DeskGroup`, and `build-installer.ps1` now produces both the Setup `.exe` and Portable `.zip` (Note F)

### v1.4.5
- ✅ Added: **Expanded first-run setup wizard** — on first launch after installation a full multi-section wizard guides you through Appearance (theme, group transparency 15–85%), Desktop Integration (hide grouped icons, shell context menu), Hotkeys (preloaded with defaults), Startup & Behaviour (start with Windows, start minimized, multi-monitor, desktop shortcut, pin to Start/taskbar), and Updates (auto-update, auto-install, bug reporting opt-in)
- ✅ Added: **Opt-in bug reporting** — new option in Settings → Developer; if enabled, a crash opens a pre-filled GitHub Issues page in your browser (no data sent automatically)
- ✅ Fixed: Desktop icons now visually refresh in Windows Explorer immediately after a grouped icon is hidden or restored on Windows 11 (`SHCNE_UPDATEDIR` folder-level notification)

### v1.4.3
- ✅ Fixed: Drag-to-reorder icons within a group now works reliably (DragEnter was incorrectly rejecting internal drag format)
- ✅ Fixed: Dragging an icon directly from one group to another now moves it correctly (cross-group transfer with desktop-restore state preserved)
- ✅ Fixed: Shortcut launch no longer silently fails after a re-install or upgrade — uses the managed `.lnk` path with shell re-resolution, falling back to desktop path then target executable

### v1.4.2
- ✅ Added: Crash logs written to `%LOCALAPPDATA%\DeskGroup\crash-reports\` on unexpected errors (max 10 files, 30-day retention)
- ✅ Added: **🐛 Report a Bug** button in the **About** tab — opens a pre-filled GitHub issue with version, OS, and .NET runtime info

### v1.4.1
- ✅ Fixed: Desktop icons now immediately update in Windows Explorer after being hidden/restored on Windows 11 (`SHChangeNotify` refresh)
- ✅ Fixed: Stale hotkeys from older versions (`Ctrl+Alt+G`) auto-migrate to the correct default (`Ctrl+F2`) on first run after upgrading
- ✅ Fixed: First tray-click after launch no longer incorrectly hides groups that were saved as hidden at last exit
- ✅ Fixed: Rename, color picker, hotkey warning, and first-run dialogs now center correctly over their parent window instead of appearing behind it
- ✅ Added: Loading cursor spinner when launching a shortcut — gives visual feedback for slow-starting apps
- ✅ Added: **Restart DeskGroup** option in the tray right-click menu (saves all groups before restarting)
- ✅ Added: Double-clicking the DeskGroup taskbar or desktop shortcut while the app is already running now shows all groups

---

## Building from Source

### Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Visual Studio 2022](https://visualstudio.microsoft.com/) (recommended) or VS Code
- Windows 10/11

### Build
```bash
git clone https://github.com/johanfourie74/DeskGroup.git
cd DeskGroup
dotnet restore src/DeskGroup/DeskGroup.csproj
dotnet build src/DeskGroup/DeskGroup.csproj --configuration Release
dotnet run --project src/DeskGroup/DeskGroup.csproj
```

### Package
```powershell
# Portable ZIP + Installer EXE (requires Inno Setup 6)
.\build\build-installer.ps1
```

### Project Structure
```
DeskGroup/
|-- src/DeskGroup/
|   |-- Models/          # Data models (ShortcutItem, IconGroup, AppSettings)
|   |-- Services/        # Business logic (persistence, icons, shortcuts, hotkeys)
|   |-- ViewModels/      # MVVM ViewModels
|   |-- Views/           # WPF Windows and UserControls
|   |-- Controls/        # Reusable UI controls (IconTile, ColorPicker, DragThumb, HotkeyBox)
|   |-- Converters/      # WPF value converters
|   |-- Helpers/         # Win32 interop, DPI, positioning utilities
|   |-- Themes/          # Light/Dark theme resource dictionaries
|   |-- Assets/          # Icons and images
|-- build/               # Build and packaging scripts
|-- README.md
```

### Data & Privacy
All data is stored locally - nothing is sent to the internet
Installed mode: %AppData%\DeskGroup\
Portable mode: DeskGroupData\ next to the executable
Desktop icons are moved, not deleted - always restorable
No telemetry, no analytics, no network requests
Contributing
Contributions are welcome! Please:

Fork the repository
Create a feature branch (git checkout -b feature/amazing-feature)
Commit your changes (git commit -m "Add amazing feature")
Push to the branch (git push origin feature/amazing-feature)
Open a Pull Request
## License

DeskGroup is licensed under the **[PolyForm Noncommercial License 1.0.0](LICENSE.txt)**.

- **Free** for personal, educational, and non-commercial use (including IT intranet deployments that do not generate revenue)
- **Commercial use** (selling, SaaS, paid products bundling DeskGroup) requires a separate commercial license
- **White-labelling** (removing or replacing author credits/branding) requires a white-label license
- Attribution — *DeskGroup by Johan Fourie* — must remain visible in all non-white-label uses

See [LICENSE.txt](LICENSE.txt) for the full non-commercial license and [COMMERCIAL_LICENSE.md](COMMERCIAL_LICENSE.md) for commercial and white-label licensing details.

Contact: **johan.fourie74@gmail.com**

Acknowledgments
CommunityToolkit.Mvvm - MVVM framework
Hardcodet.NotifyIcon.Wpf - System tray support
Inno Setup - Installer packaging
Windows 7 Gadgets - The original inspiration
Made with determination. 
