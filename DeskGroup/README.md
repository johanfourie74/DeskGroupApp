# ðŸ—‚ï¸ DeskGroup â€” Desktop Icon Organizer

**DeskGroup** is a lightweight Windows 10/11 desktop utility that lets you organize your desktop shortcuts into clean, widget-like group containers â€” similar to Windows 7 gadgets.

Clean up your cluttered desktop while keeping all your shortcuts instantly accessible.

---

## âœ¨ Features

### ðŸ”¹ Four View Modes
| Mode | Description |
|------|-------------|
| **Minimized** | Small pill/badge showing group icon + name. Default resting state. |
| **Hover** | Expands on mouse-over to show your **top 10 most-used** shortcuts. |
| **Normal** | Resizable, draggable window showing **all** shortcuts in the group. |
| **Fullscreen** | Full-screen overlay with large icon grid. Toggle with F11. |

### ðŸ”¹ Smart Organization
- **Drag & drop** shortcuts from Explorer or your desktop directly into groups
- **Right-click â†’ Add** to browse for shortcuts and applications
- **Auto-detect** new ungrouped desktop icons with gentle reminders
- **Usage tracking** â€” hover mode automatically surfaces your most-launched shortcuts

### ðŸ”¹ Desktop Integration
- **Sticky mode** â€” widgets sit on the desktop layer behind all windows (like Win7 gadgets)
- **Float mode** â€” widgets stay above all windows (like sticky notes)
- **Snap-to-grid** with configurable grid size
- **Pin position** to prevent accidental dragging
- **Multi-monitor support** with per-monitor DPI awareness

### ðŸ”¹ Customization
- **Per-group accent colors** for easy visual identification
- **Custom group icons** â€” use any `.ico`, `.exe`, or `.dll` icon
- **Light / Dark / System** theme modes (follows Windows setting)
- **Configurable icon sizes** â€” follow system default or set your own
- **Configurable hotkeys** for show/hide all and fullscreen toggle

### ðŸ”¹ Safety & Persistence
- Desktop icons are **safely hidden** (moved to managed storage), not deleted
- **Full restore** â€” ungrouping or deleting a group restores icons to their original position
- **Auto-save** with debounce (no manual saving needed)
- **Automatic backups** â€” last 5 snapshots of your group data
- **Atomic file writes** â€” no data corruption from crashes
- **Portable mode** support â€” run from a USB drive with no installation

---

## ðŸ–¥ï¸ System Requirements

- **OS:** Windows 10 (1903+) or Windows 11
- **Runtime:** .NET 6.0 (included in self-contained builds)
- **RAM:** ~30 MB
- **Disk:** ~60 MB (self-contained) / ~5 MB (framework-dependent)

---

## ðŸ“¦ Installation

### Option A: Installer (Recommended)
1. Download `DeskGroup-Setup-x.x.x.exe` from the [Releases](#) page
2. Run the installer â€” no admin rights required
3. DeskGroup starts automatically and appears in your system tray

### Option B: Portable
1. Download `DeskGroup-Portable-x.x.x.zip` from the [Releases](#) page
2. Extract anywhere (USB drive, Documents, etc.)
3. Run `DeskGroup.exe`
4. Data is stored in `DeskGroupData/` next to the executable

---

## ðŸš€ Quick Start

1. **Right-click** the system tray icon â†’ **New Group**
2. **Name** your group (e.g., "Browsers", "Games", "Work")
3. **Drag & drop** desktop shortcuts onto the group widget
4. **Hover** over the minimized pill to see your top shortcuts
5. **Double-click** any icon to launch it
6. **Repeat** â€” create as many groups as you need!

### Keyboard Shortcuts
| Shortcut | Action |
|----------|--------|
| `Ctrl+Alt+G` | Toggle all groups visible/hidden |
| `F11` | Toggle fullscreen on active group |
| `Escape` | Exit fullscreen |

*Hotkeys are fully configurable in Settings.*

---

## âš™ï¸ Settings

Access via **tray icon â†’ Settings** or right-click any group widget.

| Setting | Default | Description |
|---------|---------|-------------|
| Theme | System | Light, Dark, or follow Windows |
| Dock mode | Sticky | Sticky (desktop layer) or Float (above windows) |
| Snap to grid | On | Snap widget positions to a grid |
| Grid size | 20px | Grid spacing in pixels |
| Hide grouped icons | On | Move original desktop icons to managed storage |
| Icon size | System | Follow Windows or set custom size |
| Start with Windows | On | Auto-launch at login |
| Detect new icons | On | Watch desktop for new ungrouped shortcuts |
| Multi-monitor | On | Allow groups on all connected displays |

---

## ðŸ—ï¸ Building from Source

### Prerequisites
- [.NET 6 SDK](https://dotnet.microsoft.com/download/dotnet/6.0) or later
- [Visual Studio 2022](https://visualstudio.microsoft.com/) (recommended) or VS Code
- Windows 10/11

### Build
```bash
git clone https://github.com/yourusername/DeskGroup.git
cd DeskGroup
dotnet restore src/DeskGroup/DeskGroup.csproj
dotnet build src/DeskGroup/DeskGroup.csproj --configuration Release
dotnet run --project src/DeskGroup/DeskGroup.csproj
# Portable ZIP
.\build\build-portable.ps1

# Installer EXE (requires Inno Setup 6)
.\build\build-installer.ps1
dotnet test tests/DeskGroup.Tests/DeskGroup.Tests.csproj
DeskGroup/
â”œâ”€â”€ src/DeskGroup/
â”‚   â”œâ”€â”€ Models/          # Data models (ShortcutItem, IconGroup, AppSettings)
â”‚   â”œâ”€â”€ Services/        # Business logic (persistence, icons, shortcuts, hotkeys)
â”‚   â”œâ”€â”€ ViewModels/      # MVVM ViewModels
â”‚   â”œâ”€â”€ Views/           # WPF Windows and UserControls
â”‚   â”œâ”€â”€ Controls/        # Reusable UI controls (IconTile, ColorPicker, DragThumb)
â”‚   â”œâ”€â”€ Converters/      # WPF value converters
â”‚   â”œâ”€â”€ Helpers/         # Win32 interop, DPI, positioning utilities
â”‚   â”œâ”€â”€ Themes/          # Light/Dark theme resource dictionaries
â”‚   â””â”€â”€ Assets/          # Icons and images
â”œâ”€â”€ tests/DeskGroup.Tests/  # Unit tests (xUnit + FluentAssertions)
â”œâ”€â”€ build/               # Build and packaging scripts
â””â”€â”€ README.md
ðŸ”’ Data & Privacy
All data is stored locally â€” nothing is sent to the internet
Installed mode: %AppData%\DeskGroup\
Portable mode: DeskGroupData\ next to the executable
Desktop icons are moved, not deleted â€” always restorable
No telemetry, no analytics, no network requests
ðŸ¤ Contributing
Contributions are welcome! Please:

Fork the repository
Create a feature branch (git checkout -b feature/amazing-feature)
Commit your changes (git commit -m "Add amazing feature")
Push to the branch (git push origin feature/amazing-feature)
Open a Pull Request
ðŸ“„ License
This project is licensed under the MIT License. See LICENSE for details.

ðŸ™ Acknowledgments
CommunityToolkit.Mvvm â€” MVVM framework
Hardcodet.NotifyIcon.Wpf â€” System tray support
Inno Setup â€” Installer packaging
Windows 7 Gadgets â€” The original inspiration â¤ï¸
Made with â˜• and determination. 