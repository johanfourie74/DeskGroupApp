# 🚀 START HERE - Next Session

**Last Session Date:** 2025-07-14  
**Status:** ✅ **CLEAN BUILD — Bug D/E/M FIXED — v1.4.3 BUILT & PUSHED — GitHub Release pending (manual)**  
**Current Branch:** `Dev`  
**Latest Commit:** `241c83a` (chore: bump version to 1.4.3...)  
**Restore Point:** `241c83a` ← **USE THIS**

---

## 📍 WHERE WE ARE

### ✅ COMPLETED THIS SESSION (Session 8 — 3 bug fixes, 2 commits)

1. **Bug D ✅** (`b9ecd1f`) — `ShortcutItem.cs`: added `StoredFilePath` property. `DesktopIconService.cs`: populates `StoredFilePath` after `File.Move`. `ShortcutItemViewModel.cs`: `BuildLaunchInfo()` priority chain — 1) `StoredFilePath` (.lnk in HiddenIcons), 2) `OriginalShortcutPath` (desktop), 3) `TargetPath` (exe fallback). `Launch()` shows `MessageBox` on failure instead of swallowing silently.

2. **Bug E ✅** (`b9ecd1f`) — `GroupWidget.xaml.cs` `OnDragEnter` + new `OnDragOver`: now accept `"DeskGroupShortcut"` format with `DragDropEffects.Move`. Previously returned `None` → Drop event never fired → reorder silently failed.

3. **Bug M ✅** (`b9ecd1f`) — `GroupViewModel.cs`: `TransferShortcutToGroup()` transfers `OriginalIconState` to destination + removes from source + adds to destination (no desktop ops). `GroupWidget.xaml.cs` `OnDrop` + `OnIconDropInternal`: cross-group branch calls `TransferShortcutToGroup`; same-group branch calls existing `MoveShortcut`.

4. **Version bump ✅** (`241c83a`) — `DeskGroup.csproj` → `1.4.3`. `SettingsWindow.xaml` VersionText → `Version 1.4.3`. README changelog + BACKLOG + Help Tips all updated.

5. **Artifacts ✅** — `DeskGroup-Setup-1.4.3.exe` + `DeskGroup-Portable-1.4.3.zip` built to `build\output\`.

6. **Pushed ✅** — `origin/Dev` pushed. `public/main` (DeskGroupApp) synced. Tags `v1.4.3` pushed to both remotes.

---

## ⏳ ONE MANUAL STEP REMAINING

### 🔴 GitHub Release on DeskGroup (private) — MUST DO MANUALLY

`gh` CLI not installed. Create the release manually:

1. Go to: **https://github.com/johanfourie74/DeskGroup/releases/new**
2. Tag: `v1.4.3` (already exists)
3. Title: `v1.4.3 — Bug Fixes: cross-group drag, drag-to-reorder, stale launch path`
4. Release notes:
   ```
   ## What's fixed in v1.4.3

   - **Bug D — Stale launch path**: Shortcuts that failed to launch after reinstall/upgrade now work correctly. DeskGroup tracks the managed `.lnk` path and always re-resolves via shell rather than using a cached executable path.
   - **Bug E — Drag-to-reorder**: Dragging icons within a group to reorder them now works first time, every time. The drag-enter handler was incorrectly rejecting the internal drag format, preventing the drop event from firing.
   - **Bug M — Cross-group drag**: Drag an icon from one group window and drop it onto another — it moves cleanly, preserving desktop-restore state.
   ```
5. Attach: `build\output\DeskGroup-Setup-1.4.3.exe`
6. Attach: `build\output\DeskGroup-Portable-1.4.3.zip`
7. Click **Publish release**

> ℹ️ DeskGroupApp (public) already has the tag `v1.4.3` pushed — that's source only, no binaries there.

---

## 🎯 WHAT'S NEXT AFTER RELEASE

### Backlog items (no locked order — pick by priority):

| Priority | Item | Notes |
|---|---|---|
| High | **Bug N — Desktop icon refresh** | `SHChangeNotify` not repaint-triggering in all cases on Win 11; icons sometimes duplicate visually |
| Medium | **Note F — Installer prefs** | Inno Setup: expose key settings (autostart, view mode, hotkey) during install |
| Medium | **Note G — Alignment/snap helper** | Visual guides when dragging groups near screen edges or other groups |
| Low | **Note H — First Run wizard polish** | Review FirstRunSetupDialog UX after real-world testing |

---

## 🔧 QUICK REFERENCE COMMANDS

```powershell
# Status
cd C:\Dev\DeskGroup; git status; git log --oneline -5

# Build
.\build\build-installer.ps1
.\build\build-portable.ps1

# Push private
git push origin Dev

# Sync public
git checkout public-main
git checkout Dev -- src/ build/ README.md
git commit -m "chore: sync vX.Y.Z to public release branch"
git push public public-main:main
git tag vX.Y.Z
git push public vX.Y.Z
git push origin vX.Y.Z
git checkout Dev

# Artifacts location
build\output\DeskGroup-Setup-1.4.3.exe
build\output\DeskGroup-Portable-1.4.3.zip
```

---

## ⚠️ KNOWN ISSUES / GOTCHAS

- `UpdateService.cs` `Repo = "DeskGroup"` — **DO NOT CHANGE**. DeskGroup (private) is the release repo. DeskGroupApp is source-only.
- `gh` CLI is NOT installed — GitHub Releases must be created via browser.
- Drag-drop `"DeskGroupShortcut"` format: `IconTile.OnPreviewMouseMove` initiates; `GroupWidget.OnDragEnter/Over/Drop` accept; result `== None` removes from source.
- Position save guard: positions outside `-5000..10000` are never saved (hide/show transition artifacts).
- `_isInitializing = true` during `GroupViewModel` constructor — all `partial void OnXxxChanged` return early, so no `SyncToModel` during load.

---

## 📁 GIT STATE

```
Branch:    Dev
HEAD:      241c83a
origin:    https://github.com/johanfourie74/DeskGroup  (private, releases here)
public:    https://github.com/johanfourie74/DeskGroupApp  (source only)
Tags:      v1.4.3 on both remotes
Clean:     yes (no unstaged changes)
```

---

## 📍 WHERE WE ARE

### ✅ COMPLETED THIS SESSION (Session 7 — 7 fixes, b47fe1d)

1. **Bug J ✅** — `PersistenceService.cs`: `MigrateSettings()` resets legacy `Ctrl+Alt+G` → `Ctrl+F2` on first load after upgrade.

2. **Bug H ✅** — Dialog owner audit: `GroupWidget.xaml.cs` (`MenuRename_Click`, `MenuChangeColor_Click`), `MainViewModel.cs` (`HotkeyWarningDialog`), `App.xaml.cs` (`FirstRunSetupDialog`) — all now have `Owner = this/window` + `CenterOwner`.

3. **Note L ✅** — `ShortcutItemViewModel.cs` `Launch()`: `Mouse.OverrideCursor = Cursors.AppStarting` + 4s `DispatcherTimer` reset. Cursor cleared immediately on exception.

4. **Bug I ✅** — `DesktopIconService.cs`: Added `SHChangeNotify` P/Invoke (`SHCNE_DELETE` on hide, `SHCNE_CREATE` on restore/orphan-restore). Desktop now auto-refreshes without F5 on Win 11.

5. **Note K ✅** — `App.xaml.cs` `BuildTrayContextMenu()`: Added "Restart DeskGroup" item above Exit, with `MessageBox` confirm → `SaveAllAsync` → `Process.Start` → `Shutdown`.

6. **Note A ✅** — `--show-all` IPC wiring end-to-end: arg detection in `OnStartup`, `NamedPipeServer.TrySend("--show-all")` for second-instance, pipe receiver calls `ShowAllGroupsCommand.Execute(null)`.

7. **Bug C ✅** — `MainViewModel.InitializeAsync()`: `AllGroupsVisible` now synced from loaded group state so first tray-click toggles the correct direction.

**Build:** ✅ Clean — all 6 files, 102 insertions. Committed `b47fe1d`, pushed `origin/Dev`.

---

## 🎯 WHAT'S NEXT — LOCKED IN FOR NEXT SESSION

> ⚠️ **Do these in order. Do not skip steps.**

---

### **STEP 2 — Set up the public GitHub repository** 🌐
**Decision locked in:** Two separate repos (Option A).

| Repo | Visibility | Contents |
|---|---|---|
| `johanfourie74/DeskGroup` (current) | **Private** | Everything — source, session notes, backlog, build artifacts |
| New public repo (name TBD — suggest `DeskGroup`) | **Public** | `src/`, `build/`, `README.md`, `LICENSE` only — NO session notes, NO backlog |

**Steps to execute:**
1. Create new empty public repo on GitHub (go to github.com → New repository)
   - Name: `DeskGroup` (or `DeskGroup-App` if `DeskGroup` is taken by current private repo)
   - Set to **Public**, no README, no .gitignore (we push our own)
2. Add it as a second remote on the local machine:
   ```powershell
   git remote add public https://github.com/johanfourie74/<NEW-PUBLIC-REPO>.git
   ```
3. Create a clean branch with only public files and push it:
   ```powershell
   # We will script this step-by-step when we start
   ```
4. Create a **GitHub Release** on the public repo tagged `v1.4.1`:
   - Attach `build\output\DeskGroup-Setup-1.4.1.exe`
   - Attach `build\output\DeskGroup-Portable-1.4.1.zip`
   - Write release notes from `SESSION_SUMMARY_2025_07_12.md`
5. Enable **GitHub Issues** on the public repo (Settings → Features → Issues ✅)

> The private repo stays completely untouched. Nothing changes there.

---

### **STEP 3 — Add "Report a Bug" + crash handler (v1.4.2)** 🐛📬
**Decision locked in:** Custom lightweight approach — no third-party services.

**Part A — Manual "Report a Bug" button (About tab):**
- Add button to `SettingsWindow.xaml` About tab
- On click: open browser to pre-filled GitHub Issues URL:
  `https://github.com/johanfourie74/<PUBLIC-REPO>/issues/new?template=bug_report.md&title=...&body=...`
- Body auto-includes: App version, Windows build, .NET version, machine architecture

**Part B — Automatic crash handler:**
- Hook `App.xaml.cs` `DispatcherUnhandledException` + `AppDomain.CurrentDomain.UnhandledException`
- On crash: write log to `%LOCALAPPDATA%\DeskGroup\crash-reports\crash-<timestamp>.txt`
- Show dialog: *"DeskGroup ran into a problem. Would you like to report this?"* → Yes opens Issues URL
- Crash logs kept for 30 days, max 10 files (auto-clean)
- New file: `src\DeskGroup\Helpers\CrashReporter.cs`

> ⚠️ **Requires Step 2 to be done first** — needs the public repo Issues URL.

---

### **STEP 4 — Bump to v1.4.2 + rebuild** 🔢
After Steps 2–3 complete.

---

### **STEP 5 — Note B: Update Help panel + README for all session-7 items** 📄
Per the standing doc-update rule, all 7 completed fixes need Help panel + README updates.
Items: Bug J (hotkey migration), Bug H (dialog owners), Bug I (SHChangeNotify), Bug C (tray sync),
Note A (--show-all IPC), Note K (restart menu), Note L (cursor spinner).

---

### **STEP 6 — Feature Backlog (deferred)** 📋
See BACKLOG.md. Priority order:
- Bug M — Cross-group icon drag (High)
- Bug E — Drag-to-reorder unreliable (Medium)
- Bug D — Shortcut click unreliable (Medium)
- Note G — Window alignment tool (v1.5+)
- Note F — Installer preference pages

---

## ⚡ QUICK REFERENCE COMMANDS
```powershell
# Verify current commit
git log --oneline -5

# Build installer
.\build\build-installer.ps1

# Build portable zip  
.\build\build-portable.ps1

# Git status
git status

# Open solution
start src\DeskGroup\DeskGroup.sln
```

---

## 🗂️ KEY FILES MODIFIED (b47fe1d — session 7)

| File | Change |
|---|---|
| `src\DeskGroup\Services\PersistenceService.cs` | `MigrateSettings()` — reset legacy hotkey |
| `src\DeskGroup\Services\DesktopIconService.cs` | `SHChangeNotify` P/Invoke + 3 call sites |
| `src\DeskGroup\ViewModels\ShortcutItemViewModel.cs` | `Launch()` — AppStarting cursor + 4s timer |
| `src\DeskGroup\ViewModels\MainViewModel.cs` | `AllGroupsVisible` sync + HotkeyWarning owner |
| `src\DeskGroup\Views\GroupWidget.xaml.cs` | Rename/Color dialog owners |
| `src\DeskGroup\App.xaml.cs` | --show-all IPC, Restart menu, FirstRun owner |

└── FIX_HOVER_JUMP_WNDPROC_SOLUTION.md
```

---

## 🔑 KEY CONTEXT

### Project Specifics
- **.NET 10**, WPF, `WindowStyle=None`, `AllowsTransparency=True`
- MVVM — CommunityToolkit.Mvvm source generators (`[ObservableProperty]`, `[RelayCommand]`)
- JSON persistence to `%APPDATA%\DeskGroup\` via `PersistenceService`
- Single-instance guard (mutex) + Named Pipe IPC (`NamedPipeServer`)
- System tray via `Hardcodet.Wpf.TaskbarNotification`

### Architecture Notes
- `AllGroupsVisible` synced from loaded group state in `InitializeAsync()` (Bug C fix)
- `SHChangeNotify` added to `DesktopIconService.cs` — NOT in `Win32Interop.cs`
- `MigrateSettings()` runs once per load in `PersistenceService.LoadSettingsAsync()`
- All dialogs must have `Owner = window` + `WindowStartupLocation = CenterOwner` before showing

---

## 🚨 KNOWN ISSUES / DEFERRED BUGS

| ID | Description | Priority |
|---|---|---|
| Bug M | Cross-group icon drag fails silently | High |
| Bug E | Drag-to-reorder icons unreliable (needs 2–3 attempts) | Medium |
| Bug D | Icon shortcut click sometimes does nothing | Medium |
| Note G | Window alignment/sizing tool | Low (v1.5+) |
| Note F | Installer preference pages | Low |

---

## 📊 GIT STATUS

```
Branch: Dev
Sync: ✅ Local = Remote (b47fe1d)
Working Tree: Clean
Restore Point: b47fe1d
```

**Verify sync:**
```powershell
git status
git log --oneline -5
```

---

## 🔖 QUICK REFERENCE

**Build:** `dotnet build src\DeskGroup\DeskGroup.sln`  
**Status:** `git status`  
**History:** `git log --oneline -8`  
**Restore Point:** `git checkout b47fe1d`  
**Latest:** `git checkout Dev`  
**Sync:** `git pull origin Dev`

**Session summaries:**
- This session: `SESSION_SUMMARY_2025_07_13.md`
- Prior session: `SESSION_SUMMARY_2025_07_12.md`
- WndProc fix doc: `FIX_HOVER_JUMP_WNDPROC_SOLUTION.md`

