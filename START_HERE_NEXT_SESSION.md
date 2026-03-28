# 🚀 START HERE - Next Session

**Last Session Date:** 2025-07-12  
**Status:** ✅ **CLEAN BUILD — v1.4.1 SHIPPED — DOCS + IN-APP HELP FULLY UPDATED**  
**Current Branch:** `Dev`  
**Latest Commit:** `bc3f21d` (chore: bump to v1.4.1 — in-app help + About version fix)  
**Restore Point:** `bc3f21d` ← **USE THIS**

---

## 📍 WHERE WE ARE

### ✅ COMPLETED THIS SESSION (Phase 2 + v1.4.x docs/release)

1. **Phase 2 multi-fix** (commit `d8d290c`) — all 6 items:
   - Win32 drag fix in `SettingsWindow.xaml.cs` (WM_NCLBUTTONDOWN)
   - `HotkeyBox` — `CaptureMode` DP (KeyboardOnly/MouseOnly/All), Deactivated auto-cancel, duplicate detection
   - `SettingsViewModel` — `OwnerWindow`, 4 new hotkey props, dialog owners set, `ValidateHotkeys` updated
   - `SettingsWindow.xaml` — 2-row layout (mode ComboBox + ⌨️ + 🖱️ HotkeyBox per action)
   - `HotkeyWarningDialog.xaml` — CenterOwner
   - `HotkeyMode.cs` enum — Keyboard / Mouse / Both

2. **⚙️ Settings in group body context menu** (commit `3d89aca`)
   - Added to `GroupWidget.xaml` Window.ContextMenu + `MenuSettings_Click` handler

3. **v1.4.0 version bump + build** (commit `db6dea1`)
   - `DeskGroup-Setup-1.4.0.exe` (69 MB), `DeskGroup-Portable-1.4.0.zip` (68.6 MB)

4. **README.md + in-app Help panel updated** (commit `453ecd7`)
   - README: hotkey table corrected, new features documented, build markdown fixed
   - Help panel: ⚙️ Settings entry, mouse hotkeys section, hotkey mode + shell context menu tips

5. **v1.4.1 version bump + build** (commit `bc3f21d`)
   - About tab `VersionText` fixed (was stuck on 1.3.0)
   - `DeskGroup-Setup-1.4.1.exe` (69 MB), `DeskGroup-Portable-1.4.1.zip` (68.6 MB)
   - Both artifacts in `build\output\`

---

## 🎯 WHAT'S NEXT — LOCKED IN FOR NEXT SESSION

> ⚠️ **Do these in order. Do not skip steps.**

---

### **STEP 1 — Fix any bugs found during v1.4.1 testing** 🐛
User tested v1.4.1. Address any confirmed bugs first before publishing anything.
Suspected issues may be PC-specific — confirm before coding.

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
  - Include: stack trace, app version, OS version, timestamp
- Show dialog: *"DeskGroup ran into a problem. Would you like to report this?"*
  - **Yes** → open same pre-filled GitHub Issues URL with stack trace in body
  - **No** → close silently
- Crash logs kept for 30 days, max 10 files (auto-clean old ones)

**Files to touch:** `App.xaml.cs`, `SettingsWindow.xaml`, `SettingsWindow.xaml.cs`, new `CrashReporter.cs` helper

> ⚠️ **Requires Step 2 to be done first** — needs the public repo Issues URL.

---

### **STEP 4 — Bump to v1.4.2 + rebuild** 🔢
After Steps 1–3 are complete, bump version and produce new installers.

---

### **Priority 5: Feature Backlog** 📋
Only after Steps 1–4. See BACKLOG.md. Top candidates:
- Configurable hover delay (currently 300ms hardcoded)
- Drag shortcuts between groups
- Visual feedback during drag (highlight drop zones)

---

## ⚡ QUICK REFERENCE COMMANDS
```powershell
# Build installer
.\build\build-installer.ps1

# Build portable zip
.\build\build-portable.ps1

# Git status
git log --oneline -5
git status

# Open solution
start src\DeskGroup\DeskGroup.sln
```

---
```
DeskGroup/
├── src/DeskGroup/
│   ├── Models/
│   │   └── IconGroup.cs              ← GridViewWidth/Height added here
│   ├── Views/
│   │   ├── GroupWidget.xaml.cs       ← All sizing logic, view switching
│   │   ├── SettingsWindow.xaml       ← Tabbed settings UI
│   │   └── SettingsWindow.xaml.cs    ← Tab switching, F1 hotkey
│   ├── ViewModels/
│   │   ├── GroupViewModel.cs         ← GridViewWidth/Height observable props
│   │   ├── MainViewModel.cs
│   │   └── SettingsViewModel.cs
│   ├── Controls/
│   │   └── IconTile.xaml.cs
│   └── Helpers/
│       └── Win32Interop.cs           ← WndProc hover jump fix
├── BACKLOG.md
├── SESSION_SUMMARY_SIZING_GRID_FIX.md   ← This session
├── SESSION_SUMMARY_SETTINGS_WINDOW_COMPLETE.md
└── FIX_HOVER_JUMP_WNDPROC_SOLUTION.md
```

---

## 🔑 KEY CONTEXT

### 1. GridViewWidth/Height — New in This Session
- `IconGroup.GridViewWidth` (default 400) and `GridViewHeight` (default 300)
- JSON-backward-compatible (existing saves get defaults)
- `GroupViewModel` exposes as `[ObservableProperty]`
- `GroupWidget.xaml.cs` — `OnSizeChanged` routes by view mode (Normal vs Grid)
- `ApplyViewMode(Grid)` reads from `GridViewWidth`/`GridViewHeight`
- `EnterFullscreen` saves/restores per `_preFullscreenViewMode`

### 2. Sizing Pattern (Critical — do not change)
```csharp
// CORRECT pattern for auto-sizing views:
UpdateLayout();
SomeView.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
int targetH = (int)Math.Ceiling(SomeView.DesiredSize.Height);
// NO additive padding — Math.Ceiling already handles sub-pixel
```

### 3. Hover Jump Fix (WndProc — still working)
- `_hasPendingPosition = true` → change visibility → WndProc intercepts WM_WINDOWPOSCHANGING
- Documented in `FIX_HOVER_JUMP_WNDPROC_SOLUTION.md`

### 4. Project Specifics
- **.NET 10**, WPF, `WindowStyle=None`, `AllowsTransparency=True`
- MVVM — ViewModels hold state, Views are UI only
- JSON persistence to AppData via `PersistenceService`
- CommunityToolkit.Mvvm source generators for `[ObservableProperty]`

---

## 🧪 HOW TO TEST

```bash
dotnet build
dotnet run --project src/DeskGroup/DeskGroup.csproj
```

**Sizing fix checklist:**
1. Create a group → set to Minimized → switch to Normal → switch back to Minimized  
   **Expected:** Correct minimized height immediately, no hover cycle needed
2. Hover over a Minimized group  
   **Expected:** No blank gap below footer text
3. Set group to Compact  
   **Expected:** Correct compact height, no blank gap below footer
4. Resize in Normal view → switch to Grid → resize in Grid → switch back to Normal  
   **Expected:** Each view remembers its own size independently
5. Enter fullscreen → press Escape/restore  
   **Expected:** Returns to correct pre-fullscreen size and view mode

---

## 🚨 KNOWN ISSUES / GOTCHAS

### None Currently ✅
- Sizing fixes: applied and build-verified (`457c79d`)
- Hover jump: fixed (`FIX_HOVER_JUMP_WNDPROC_SOLUTION.md`)
- Startup crash: fixed (defensive coding in SettingsWindow)
- Settings window: working with tabs

### Things to Watch
- **Sizing fixes need user runtime testing** — build passes, not yet runtime-verified by user
- **`partial void` methods with CommunityToolkit** — always include `(TypeName paramName)` in the signature or the source generator can't match and will cascade errors

---

## 📊 GIT STATUS

```
Branch: Dev
Sync: ✅ Local = Remote (90bd9b3)
Working Tree: Clean
Restore Point: 90bd9b3
```

**Verify sync:**
```bash
git status
git log --oneline -5
```

---

## 🔖 QUICK REFERENCE

**Build:** `dotnet build`  
**Run:** `dotnet run --project src/DeskGroup/DeskGroup.csproj`  
**Status:** `git status`  
**History:** `git log --oneline -8`  
**Restore Point:** `git checkout 90bd9b3`  
**Latest:** `git checkout Dev`  
**Sync:** `git pull origin Dev`  

**Documentation:**
- Task List: `BACKLOG.md`
- This Session: `SESSION_SUMMARY_HELP_PANEL_POLISH.md`
- Sizing Session: `SESSION_SUMMARY_SIZING_GRID_FIX.md`
- Settings Session: `SESSION_SUMMARY_SETTINGS_WINDOW_COMPLETE.md`
- WndProc Fix: `FIX_HOVER_JUMP_WNDPROC_SOLUTION.md`
- This Guide: `START_HERE_NEXT_SESSION.md`
