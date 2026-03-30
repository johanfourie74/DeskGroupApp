# DeskGroup - Development Backlog

**Last Updated:** 2026-03-31 (session 12 — Bug C/H/I + Note A/L complete)
**Current release:** v1.4.5 (live)
**Pending release:** v1.4.6 (local, not yet pushed)

---

## 🔴 STANDING RULE — DOC UPDATE POLICY

> **Every user-facing feature or fix MUST update the Help panel + README changelog + BACKLOG in the same commit. No exceptions.**

This applies to: new features, bug fixes, UI changes, behaviour changes, settings additions.

---

## 🔜 DO NEXT (in order)

| Priority | ID | Est. | Description |
|---|---|---|---|
| ✅ Done | **Bug C** | — | Tray icon first-click — fixed: `ToggleAllGroupsVisibility()` rewritten with 4-state logic based on `SavedDockModeBeforeFloat`+`DockMode`, not stale `AllGroupsVisible` flag. |
| ✅ Done | **Bug H** | — | Secondary dialogs behind parent — fixed: `OpenFileDialog.ShowDialog(this)` in `MenuAddShortcut_Click` and `MenuChangeIcon_Click`. |
| ✅ Done | **Bug I** | — | Desktop icon not hiding on Win 11 — fixed: `HideGroupedIcons` guard added to `AddShortcut`; `SHCNE_ASSOCCHANGED` broadcast added after `SHCNE_UPDATEDIR`. |
| ✅ Done | **Note A** | — | Jump List — implemented in `App.xaml.cs OnStartup`: New Group, Show/Hide All, Settings, Exit. IPC extended for `--new-group`, `--show-settings`, `--exit`. |
| ✅ Done | **Note L** | — | Launch cursor spinner — already present in `ShortcutItemViewModel.LaunchAsync()` (v1.4.1). |
| 🟡 Medium | **Note F** | 2–3 hrs | Installer questionnaire: surface autostart, view mode, dock, hotkey, install directory choice during Inno Setup install. |
| 🟡 Medium | **Note G** | 5–7 hrs | Group alignment / sizing tool (see full spec below). |

---
## 📐 DETAILED SPECS

### Note A — Functional desktop/taskbar icon
**Goal:** DeskGroup.lnk on the desktop and taskbar behaves intelligently when the app is already running.

| Scenario | Behaviour |
|---|---|
| App not running, double-click icon | Normal launch |
| App running, left-click icon (desktop) | Show all groups (same as `Ctrl+F2`) |
| App running, right-click icon (desktop/taskbar) | Show Jump List with tray context menu items (New Group, Show/Hide All, Settings, Exit) |
| App running, double-click icon | Show all groups (already implemented via `--show-all` IPC) |

**Implementation notes:**
- `SingleInstanceGuard` + `NamedPipeServer` already exist in `App.xaml.cs` — `--show-all` signal is already wired
- Left-click on desktop shortcut = double-click a `.lnk` = launches new process → single-instance guard catches it and sends `--show-all` via named pipe — **already works**
- Jump List: `System.Windows.Shell.JumpList` + `JumpTask` items; populate in `App.xaml.cs` `OnStartup`
- Taskbar overlay / toolbar pinning behaviour is automatic if Jump List is set up correctly

---

### Note G — Group alignment / sizing tool
**Goal:** Multi-select group widgets and align or resize them relative to the first-selected group (anchor).

**Alignment operations (9-button panel, similar to VS alignment toolbar):**

| Button | Action |
|---|---|
| Align Left | Left edges match anchor's left edge |
| Align Centre (H) | Horizontal centres match anchor's centre |
| Align Right | Right edges match anchor's right edge |
| Align Top | Top edges match anchor's top edge |
| Align Centre (V) | Vertical centres match anchor's centre |
| Align Bottom | Bottom edges match anchor's bottom edge |
| Distribute Horizontally | Equal horizontal spacing between selected groups |
| Distribute Vertically | Equal vertical spacing between selected groups |
| Make Same Width | All selected groups resized to anchor's width |
| Make Same Height | All selected groups resized to anchor's height |

**Rules:**
- The **first selected group** is always the anchor — all others align/size to it
- Operations use `SetWindowPos(HWND, HWND_TOP, x, y, w, h, flags)` via `Win32Interop`
- Multi-select: hold `Ctrl` and click group title bars, or rubber-band select
- Alignment panel: a small floating toolbar that appears when ≥ 2 groups are selected (or accessible via right-click context menu on a group)

**Help panel note:** Document in Help tab — "Select multiple groups (Ctrl+click) then use the alignment toolbar to snap them to the same position, size, or spacing. The first group you select is always the anchor."

---

## 📋 FEATURE BACKLOG (pending)

### Performance & Optimization ⚡
- [ ] Profile memory usage during drag operations
- [ ] Optimize icon caching strategy
- [ ] Investigate potential memory leaks in long-running sessions
- [ ] Reduce layout recalculations during view mode switches
- [ ] Optimize transparency/opacity updates

### User Experience 👤
- [ ] Configurable hover delay (currently 300 ms hardcoded)
- [ ] Option to disable auto-collapse on hover mode
- [ ] Keep hover-mode expanded when clicking inside group
- [ ] Search within groups (Ctrl+F)
- [ ] Cycle through groups (Tab/Shift+Tab when focused)
- [ ] Quick access to most-used group (e.g. Win+1)
- [ ] Visual feedback during drag (highlight drop zones)
- [ ] Drag to ungrouped icons dialog to remove icon from group

### View Modes 👁️
- [ ] Grid view: configurable columns (auto / 2 / 3 / 4 / 5)
- [ ] Grid view: show/hide shortcut labels toggle
- [ ] Minimized view: show most-used icon instead of group icon
- [ ] Minimized view: animated notification when new shortcut added

### Customization 🎨
- [ ] Custom font sizes (global setting)
- [ ] Custom border styles (rounded / square / thick / thin)
- [ ] Save/load layout presets (export/import groups)

### Smart Features 🧠
- [ ] Duplicate shortcut detection across groups
- [ ] Usage statistics dashboard (most-used groups/shortcuts)
- [ ] Time-based group visibility (work hours vs. evening)

### Integration 🔗
- [ ] OneDrive / custom-folder cloud sync for groups JSON
- [ ] Windows 11 Widgets integration (future)
- [ ] Steam / Epic Games library integration (future)

### Accessibility ♿
- [ ] High contrast mode support
- [ ] Full keyboard navigation (no mouse required)
- [ ] Screen reader (Narrator / NVDA) compatibility

### Stability & Reliability 🛡️
- [ ] Manual backup/restore UI (currently auto-5-snapshots only)
- [ ] Export groups to portable/shareable format
- [ ] Graceful broken-icon placeholder (no silent failure when shortcut file is missing)

### Installer 🛠️
- [ ] Inno Setup first-run preference pages (Start with Windows, default view mode, hotkey)

### Technical Debt 🔧
- [ ] Remove or document test menu items ("Force All Visible", "Test Window")
- [ ] Standardise naming conventions across ViewModels
- [ ] Extract magic numbers to constants
- [ ] Refactor Win32Interop into service pattern
- [ ] Unit tests for ViewModels
- [ ] Integration tests for persistence
- [ ] CI/CD pipeline (GitHub Actions)
- [ ] Code signing certificate

---

## 📝 BACKLOG MAINTENANCE

With every commit:
1. Mark completed tasks `[x]`
2. Update "Last Updated" + "Pending release" at the top
3. Add new items as they are discovered
4. Remove/archive stale items that will never be built
5. **Update Help panel + README changelog in the same commit as any user-facing change** ← standing rule
