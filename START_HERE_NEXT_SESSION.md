# 🚀 START HERE - Next Session

**Last Session Date:** 2026-03-31
**Status:** ✅ **CLEAN — v1.4.5 LIVE — Both repos & releases fully up to date**
**Current Branch:** `Dev`
**Latest Commit:** `34d9a2c` (v1.4.5 — First-run setup wizard, opt-in bug reporting, Win11 desktop refresh fix)
**Restore Point:** `34d9a2c` ← **USE THIS**

---

## 📍 WHERE WE ARE

### ✅ SESSIONS 10+11 (2026-03-31) — v1.4.5 full implementation + release

1. **First-run setup wizard — full rewrite ✅** (`34d9a2c`) — `FirstRunSetupDialog.xaml` (471 lines, 5-section wizard: Appearance, Desktop Integration, Hotkeys, Startup & Behaviour, Updates). `FirstRunSetupDialog.xaml.cs` initialises all 20 controls; `GetStarted_Click` saves + applies side-effects (StartupService, ShellContextMenu, ShortcutInstallHelper).

2. **Opt-in bug reporting ✅** (`34d9a2c`) — `AppSettings.OptInBugReporting` (default false). Wired in `SettingsViewModel` ([ObservableProperty] + constructor + Apply + ResetToDefaults). `SettingsWindow.xaml` Developer section checkbox. `App.xaml.cs` `ShowCrashDialog` now gated on `OptInBugReporting == true`.

3. **Bug N (Win11 desktop refresh) ✅** (`34d9a2c`) — `DesktopIconService.cs` switched to `SHCNE_UPDATEDIR` on the Desktop folder. Needs real-device validation — may still have issues.

4. **Version 1.4.5 ✅** — `DeskGroup.csproj`, `SettingsWindow.xaml` VersionText, README, BACKLOG all updated.

5. **Build fix ✅** — .NET 10 WPF `System.Windows.ThemeMode` ambiguity resolved by qualifying as `(Models.ThemeMode)CbTheme.SelectedIndex` in `FirstRunSetupDialog.xaml.cs`.

6. **Both GitHub releases ✅** — `johanfourie74/DeskGroup` (private) + `johanfourie74/DeskGroupApp` (public) — v1.4.5 Latest, both `DeskGroup-Setup-1.4.5.exe` and `DeskGroup-Portable-1.4.5.zip` attached.

### ✅ SESSION 9 (2026-03-30) — Short session, no code changes

1. GitHub Release v1.4.3 confirmed. `gh` CLI installed + authenticated. `copilot-instructions.md` updated.

---

## 🎯 WHAT'S NEXT

### Bugs (marked fixed in log — VERIFY on real device):
| ID | Description | Status |
|---|---|---|
| **Bug N** | Win11 desktop icon refresh — `SHCNE_UPDATEDIR` applied, needs real-device test | Verify |

### Feature backlog (priority order):
| Priority | ID | Description |
|---|---|---|
| 🔴 High | **Note F** | Fix `build-installer.ps1` to always produce portable ZIP (currently only in fallback else-branch) |
| 🟡 Medium | **Note G** | Snap guides: visual alignment lines when dragging groups near screen edges or other groups |
| 🟡 Medium | **Note H** | First-run wizard UX review after real-world testing |
| 🟢 Low | **Note I** | Configurable hover delay (currently 300 ms hardcoded) |

---

## 🔧 QUICK REFERENCE COMMANDS

```powershell
# Verify state
cd C:\Dev\DeskGroup; git status; git log --oneline -5

# Build (portable ZIP must be built separately — build-installer.ps1 does NOT produce it when Inno Setup found)
.\build\build-installer.ps1
dotnet publish src/DeskGroup/DeskGroup.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true --output build\publish-portable
Compress-Archive -Path build\publish-portable\* -DestinationPath "build\output\DeskGroup-Portable-X.Y.Z.zip" -Force

# Push private
git push origin Dev

# gh CLI (not in default PATH — add manually each session)
$env:PATH += ";C:\Program Files\GitHub CLI"

# gh release create (tag must exist locally first: git fetch origin --tags)
gh release create vX.Y.Z "build\output\DeskGroup-Setup-X.Y.Z.exe" "build\output\DeskGroup-Portable-X.Y.Z.zip" --repo johanfourie74/DeskGroup --title "vX.Y.Z — ..." --notes-file build\release-notes.md --latest
gh release create vX.Y.Z "build\output\DeskGroup-Setup-X.Y.Z.exe" "build\output\DeskGroup-Portable-X.Y.Z.zip" --repo johanfourie74/DeskGroupApp --title "vX.Y.Z — ..." --notes-file build\release-notes.md --latest
```

---

## ⚠️ KNOWN GOTCHAS

- `UpdateService.cs` `Repo = "DeskGroup"` — **DO NOT CHANGE**. DeskGroup (private) is the release repo. DeskGroupApp is source-only.
- `gh` CLI NOT in default PowerShell PATH — add `$env:PATH += ";C:\Program Files\GitHub CLI"` each session.
- `gh release create` creates tag on GitHub but NOT locally — always `git fetch origin --tags` before `git push public vX.Y.Z`.
- `build-installer.ps1` NEVER produces portable ZIP when Inno Setup found — always build separately.
- In .NET 10 WPF code-behind, always qualify as `(Models.ThemeMode)` — `System.Windows.ThemeMode` causes CS0104 ambiguity.
- Drag-drop `"DeskGroupShortcut"` format: `IconTile.OnPreviewMouseMove` initiates; `GroupWidget.OnDragEnter/Over/Drop` accept.
- Position save guard: positions outside `-5000..10000` are never saved.
- `_isInitializing = true` during `GroupViewModel` constructor — all `partial void OnXxxChanged` return early.

---

## 📁 GIT STATE

```
Branch:    Dev
HEAD:      34d9a2c
origin:    https://github.com/johanfourie74/DeskGroup  (private, releases here)
public:    https://github.com/johanfourie74/DeskGroupApp  (source only)
Tags:      v1.4.5 on both remotes
Clean:     yes
```

---

## 🔑 KEY CONTEXT

- **.NET 10**, WPF, `WindowStyle=None`, `AllowsTransparency=True`
- MVVM — CommunityToolkit.Mvvm source generators (`[ObservableProperty]`, `[RelayCommand]`)
- JSON persistence to `%APPDATA%\DeskGroup\` via `PersistenceService`
- Single-instance guard (mutex) + Named Pipe IPC (`NamedPipeServer`)
- System tray via `Hardcodet.Wpf.TaskbarNotification`
- `SHChangeNotify` lives in `DesktopIconService.cs` (not `Win32Interop.cs`)
- `MigrateSettings()` runs once per load in `PersistenceService.LoadSettingsAsync()`
- All dialogs must have `Owner = window` + `WindowStartupLocation = CenterOwner`
- `BuildLaunchInfo()` priority: StoredFilePath → OriginalShortcutPath → TargetPath

### Version Formula (every release)
1. Bump `<Version>` in `DeskGroup.csproj`
2. Update VersionText in `SettingsWindow.xaml`
3. Update `README.md` changelog + `BACKLOG.md` header
4. Build installer + portable separately
5. `git commit + git push origin Dev`
6. `git fetch origin --tags` → `git push public vX.Y.Z`
7. `gh release create` on both repos

---

## 🗂️ SESSION SUMMARIES

- Sessions 10+11: `SESSION_SUMMARY_2026_03_31.md`
- Session 9: `SESSION_SUMMARY_2026_03_30.md`
- Session 8: `SESSION_SUMMARY_2025_07_14.md`
- Session 7: `SESSION_SUMMARY_2025_07_13.md`
- Session 6: `SESSION_SUMMARY_2025_07_12.md`
