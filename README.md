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

---

## Changelog

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