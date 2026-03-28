# DeskGroup - Development Backlog

**Last Updated:** 2025-07-12  
**Status:** ✅ **v1.4.1 SHIPPED — NEXT: public repo setup + bug reporting (v1.4.2)**  
**Commit:** `7189b1b`

---

## 🔜 LOCKED IN — DO THESE NEXT SESSION (in order)

### 1. Fix any v1.4.1 test bugs 🐛
Address confirmed bugs from user testing before publishing anything publicly.

### 2. Set up public GitHub repo 🌐
- Create new **public** GitHub repo (name TBD, suggest `DeskGroup`)
- Add as second git remote: `git remote add public <url>`
- Push only `src/`, `build/`, `README.md`, `LICENSE` — NO session notes/backlog
- Create GitHub Release `v1.4.1` with installer + portable zip attached
- Enable GitHub Issues on public repo
- See `START_HERE_NEXT_SESSION.md` for full step-by-step

### 3. Add "Report a Bug" + crash handler → v1.4.2 📬
**Manual (About tab button):**
- Button opens browser → pre-filled GitHub Issues URL
- Auto-includes: app version, Windows build, .NET version, architecture

**Automatic (crash handler):**
- Hook `DispatcherUnhandledException` + `AppDomain.UnhandledException` in `App.xaml.cs`
- Write crash log → `%LOCALAPPDATA%\DeskGroup\crash-reports\crash-<timestamp>.txt`
- Show "Would you like to report this?" dialog → Yes opens pre-filled GitHub Issues URL
- Keep logs 30 days, max 10 files (auto-clean)
- New file: `src\DeskGroup\Helpers\CrashReporter.cs`
- Touch: `App.xaml.cs`, `SettingsWindow.xaml`, `SettingsWindow.xaml.cs`

### 4. Bump to v1.4.2 + rebuild 🔢
After Steps 1–3 complete.

---

## 📝 BACKLOG MAINTENANCE INSTRUCTIONS

**This file MUST be updated with every commit:**
1. ✅ Mark tasks as **DONE** when completed (change `[ ]` to `[x]`)
2. ✅ Add **✓ VERIFIED** next to done items after testing
3. ✅ Update "Last Updated" date at top
4. ✅ Add new tasks as they're discovered
5. ✅ Remove or archive obsolete tasks
6. ✅ This file is included in restore point routine

---

## 🎯 CURRENT PRIORITY

### **1. UI Polish & Enhancement** 🎨
Based on new features added in BATCH 2 (Status Footer, Transparency Controls, Extended Colors)

#### **Specific Tasks:**
- [x] **Icon Hover Effect** ✓ **DONE & VERIFIED**
  - Integrated IconTile into all view modes (Hover, Compact, Normal, Grid, Fullscreen)
  - Added AccentColor property (uses group color, not standard blue)
  - Hover opacity set to 30% for subtle effect
  - Mouse event handlers working reliably

- [x] **All Dialogs Always On Top** ✓ **DONE & VERIFIED**
  - SettingsWindow, UngroupedIconsDialog, ColorPickerDialog, InputDialog
  - All dialogs now appear above floating group windows

- [x] **Transparency Control UX** ✓ **DONE & VERIFIED**
  - Added visual tick marks at 0%, 25%, 50%, 75%, 100%
  - Mouse wheel support for adjusting slider
  - Large change step set to 25%

- [x] **Settings Window Polish** ✓ **DONE & VERIFIED**
  - Added visual separators between sections
  - Improved spacing and margins
  - Window position/size persistence

- [x] **Compact & Normal View Refinements** ✓ **DONE & VERIFIED**
  - Compact view: Icons only (32x32 tiles, 24px icons) with tooltips
  - Normal view: Reduced row height (32px), even margins (2px all sides)
  - Border padding: 10px → 4px (both views)
  - Fixed IconTile height (removed FallbackValue=88)
  - Symmetric icon padding (2px top/bottom)
  - Dynamic height for 1-2 rows in Compact view
  - Tooltips conditional on ShowLabel property

- [x] **Hover Jump Fix** ✓ **DONE & VERIFIED** 🎉
  - **FIXED using WndProc WM_WINDOWPOSCHANGING interception**
  - Intercepts window position changes at Win32 level BEFORE rendering
  - Overrides incorrect (0,0) position before any frame is painted
  - No more flash/jump when expanding Minimized → Hover
  - Documented in `FIX_HOVER_JUMP_WNDPROC_SOLUTION.md` for reuse
  - **8 rounds of debugging, finally solved!**

- [x] **Settings Window Tabs (About & Help)** ✓ **DONE & VERIFIED** 🎉
  - **Tabbed interface:** Settings | About | Help (F1)
  - Dynamic header title/subtitle based on active tab
  - About tab: Version info, author, GitHub/Contact links
  - Help tab: Quick start, view modes, dock modes, keyboard shortcuts, tips
  - F1 hotkey opens Help tab from anywhere in Settings window
  - Bottom buttons (Apply/Reset) only visible on Settings tab
  - NOT in tray menu (cleaner UX - all in one Settings window)
  - **Defensive coding:** Null checks, deferred initialization, safe resource lookup
  - **Crash fix:** Prevents exit code 0xFFFFFFFF on startup
  - **Commits:** e345d47 (feature), 29db853 (crash fix)
  - **RESTORE POINT:** 29db853

- [x] **Footer Styling Polish** ✓ **DONE & VERIFIED** 🎉
  - Unified background opacity `#28000000` across all 6 view modes
  - Added subtle 1px top separator (`#30FFFFFF`) for visual anchoring
  - Added `Cursor=Hand` to all footers (was only on Compact)
  - Standardised font: 10px small views, 11px fullscreen (was 9/10/12 mixed)
  - Standardised `Foreground #CCFFFFFF` everywhere (was White/#CC mixed)
  - Consistent padding/margin across all views
  - Compact footer structure standardised to Run/SemiBold pattern

- [x] **View Sizing & Footer Gap Fixes** ✓ **DONE** — commits `deb0342`, `457c79d`
  - Footer blank space: `+20`/`+4` → `Math.Ceiling(DesiredSize)` (no additive padding)
  - `UpdateLayout()` before every `Measure()` so DesiredSize is never stale on view switch
  - Minimized height now snaps correctly on switch (no hover cycle required)
  - Compact height now snaps correctly when switching from larger views
  - Hover/Compact footer gap eliminated

- [x] **Independent Normal/Grid View Sizes** ✓ **DONE** — commit `457c79d`
  - Added `GridViewWidth`/`GridViewHeight` to `IconGroup` model (defaults 400×300, backward-compatible)
  - `GroupViewModel` exposes new observable properties, wired through `SyncToModel`
  - `GroupWidget.xaml.cs` routes `OnSizeChanged` and `ApplyViewMode` separately per view mode
  - `EnterFullscreen`/`RestoreFromFullscreen` routes to correct per-view properties
  - Normal and Grid views now remember their own independent sizes

- [x] **Fullscreen Restore Ordering Fix** ✓ **DONE** — commit `deb0342`
  - `ViewMode = Fullscreen` now set BEFORE `Width`/`Height` in `EnterFullscreen()`
  - Prevents pre-fullscreen size from being overwritten before it can be saved

- [x] **About Dialog** ✓ **DONE & VERIFIED** (Settings → About tab)
  - Version info dynamically loaded from assembly
  - Copyright, GitHub link, Contact (→ GitHub Issues)
  - Accessible from Settings window About tab

- [x] **Help Dialog (F1)** ✓ **DONE & VERIFIED** (Settings → Help tab)
  - Quick start, view modes, dock modes, keyboard shortcuts, tips
  - F1 hotkey opens Help tab from anywhere in Settings
  - Accessible from Settings window Help tab

- [x] **Color Picker Enhancement** ✓ **DONE** (v1.0 scope)
  - Removed White, Gray, Black presets (did not render correctly)
  - Palette now shows 22 Windows 11 accent colours only
  - Custom hex input still available for any colour

- [x] **Settings Close Button Polish** ✓ **DONE** — this session
  - Height reduced to `34px`, `VerticalAlignment=Top` to pin to corner
  - Negative top margin (`Margin="0,-16,0,0"`) cancels header padding
  - `CornerRadius` corrected to `0,10,0,0` to match outer window border

- [x] **Global Hotkey Default Fixed** ✓ **DONE** — this session
  - Changed default `ToggleAllHotkey` from `Ctrl+Alt+G` → `Ctrl+F2` in `AppSettings.cs`
  - Updated help text and format hint in `SettingsWindow.xaml` to match

- [x] **Help Panel Comprehensive Update** ✓ **DONE** — this session
  - **Quick Start:** Added step 4 (View Mode selection)
  - **View Modes:** Updated all descriptions — resize grip, Hover top-5, auto-collapse, independent sizes tip
  - **Right-Click Menu:** New section documenting all 7 context menu actions (View Mode, Add shortcut, Appearance, Position, Rename, Show/Hide All, Delete)
  - **Tray Icon:** New section documenting single/double-click toggle + all 6 right-click items
  - **Tips & Tricks:** Expanded from 8 → 12 tips (added double-click to pin, drag-to-add, resize grip, mouse-wheel slider, footer click restores sticky, auto-save backups)

- [x] **Phase 2 — Hotkey keyboard+mouse separation** ✓ **DONE** — commit `d8d290c`
  - `HotkeyMode.cs` enum: Keyboard / Mouse / Both
  - `AppSettings.cs`: 4 new fields (ToggleAllMouseHotkey, ToggleAllHotkeyMode, FullscreenMouseHotkey, FullscreenHotkeyMode)
  - `HotkeyBox`: CaptureMode DP, Deactivated auto-cancel, duplicate detection
  - `SettingsWindow.xaml`: 2-row layout per action (mode ComboBox + ⌨️ + 🖱️ boxes)
  - `MainViewModel.RegisterHotkeys()` mode-aware
  - `SettingsViewModel`: OwnerWindow, 4 new props, dialog owners set, ValidateHotkeys updated

- [x] **⚙️ Settings in group body context menu** ✓ **DONE** — commit `3d89aca`
  - Window.ContextMenu in GroupWidget.xaml + MenuSettings_Click handler

- [x] **README.md + in-app Help panel fully updated for v1.4.0** ✓ **DONE** — commit `453ecd7`
  - README: hotkey table, new features, build markdown all corrected
  - Help panel: Settings entry, mouse hotkeys, hotkey mode tip, shell context menu tip

- [x] **v1.4.1 — About version fix + in-app help rebuild** ✓ **DONE** — commit `bc3f21d`
  - About tab VersionText fixed (was frozen at 1.3.0)
  - Both artifacts: `DeskGroup-Setup-1.4.1.exe` (69 MB), `DeskGroup-Portable-1.4.1.zip` (68.6 MB)

---

## 📦 ARCHIVED: DETAILED FEATURE BACKLOG

**Note:** These items will be re-prioritized based on Phase 1 user feedback.  
**Moved here to keep active backlog focused on Phase 1 release.**

---

## 📋 FEATURE BACKLOG (By Category)

### **Performance & Optimization** ⚡
- [ ] **Memory Optimization**
  - Profile memory usage during drag operations
  - Optimize icon caching strategy
  - Investigate potential memory leaks in long-running sessions
  
- [ ] **Rendering Performance**
  - Reduce layout recalculations during view mode switches
  - Optimize transparency/opacity updates
  - Consider GPU acceleration for animations

### **User Experience** 👤
- [ ] **Keyboard Shortcuts**
  - [ ] Global hotkey for Show/Hide All (currently only tray menu)
  - [ ] Cycle through groups (Tab/Shift+Tab when focused)
  - [ ] Quick access to most-used group (e.g., Win+1)
  - [ ] Search within groups (Ctrl+F)
  
- [ ] **Hover Behavior Refinement**
  - [ ] Configurable hover delay (currently 300ms hardcoded)
  - [ ] Option to disable auto-collapse
  - [ ] Keep expanded when clicking inside group
  
- [ ] **Drag & Drop Enhancements**
  - [ ] Visual feedback during drag (highlight drop zones)
  - [ ] Drag shortcuts between groups
  - [ ] Reorder shortcuts within groups
  - [ ] Drag to ungrouped icons dialog to remove

### **View Modes** 👁️
- [ ] **Grid View Enhancements**
  - [ ] Configurable columns (auto, 2, 3, 4, 5)
  - [ ] Icon size slider (small, medium, large)
  - [ ] Show/hide shortcut labels
  
- [ ] **Minimized View Options**
  - [ ] Show shortcut count badge
  - [ ] Show most-used icon instead of group icon
  - [ ] Animated notification when new shortcut added
  
- [ ] **New View Mode: Compact**
  - Smaller than Normal, larger than Hover
  - Shows 4-6 icons in fixed grid
  - No auto-collapse

### **Customization** 🎨
- [ ] **Theme System**
  - [ ] Custom accent colors per group (DONE ✓)
  - [ ] Custom window transparency (DONE ✓)
  - [ ] Custom font sizes
  - [ ] Custom border styles (rounded, square, thick, thin)
  
- [ ] **Icon Management**
  - [ ] Bulk icon extraction tool
  - [ ] Icon library/gallery
  - [ ] Download icons from web
  - [ ] Generate icons from text/emoji
  
- [ ] **Layout Presets**
  - [ ] Save/load layout configurations
  - [ ] Quick switch between presets
  - [ ] Export/import layouts

### **Smart Features** 🧠
- [ ] **Auto-Organization**
  - [ ] AI-powered grouping suggestions based on usage patterns
  - [ ] Automatic categorization (Work, Games, Utilities, etc.)
  - [ ] Duplicate shortcut detection
  
- [ ] **Usage Analytics**
  - [ ] Usage statistics dashboard
  - [ ] Most-used groups/shortcuts report
  - [ ] Time-based patterns (work hours vs. evening)
  - [ ] Export data to CSV
  
- [ ] **Context-Aware Behavior**
  - [ ] Different layouts for different monitors
  - [ ] Time-based group visibility (show work groups during work hours)
  - [ ] App-triggered visibility (show dev tools when IDE opens)

### **Integration** 🔗
- [ ] **Cloud Sync**
  - [ ] OneDrive integration
  - [ ] Dropbox integration
  - [ ] Google Drive integration
  - [ ] Custom sync folder
  
- [ ] **Platform Integration**
  - [ ] Windows 11 Widgets integration
  - [ ] Task Manager integration (monitor resource usage)
  - [ ] Windows Search integration
  - [ ] Start Menu integration
  
- [ ] **Third-Party Apps**
  - [ ] Steam library integration
  - [ ] Epic Games launcher integration
  - [ ] Microsoft Store apps
  - [ ] Browser bookmark import

### **Accessibility** ♿
- [ ] **Screen Reader Support**
  - [ ] Narrator compatibility
  - [ ] JAWS support
  - [ ] NVDA support
  
- [ ] **Visual Accessibility**
  - [ ] High contrast mode
  - [ ] Larger text options
  - [ ] Color blindness modes
  - [ ] Screen magnifier compatibility
  
- [ ] **Input Accessibility**
  - [ ] Full keyboard navigation
  - [ ] Voice command support (via Windows Speech Recognition)
  - [ ] Mouse-free operation mode

### **Stability & Reliability** 🛡️
- [ ] **Error Handling**
  - [ ] Graceful degradation when icons fail to load
  - [ ] Automatic recovery from crashes
  - [ ] Detailed error logging
  - [ ] Send crash reports (opt-in)
  
- [ ] **Data Protection**
  - [ ] Incremental backups (currently 5 snapshots)
  - [ ] Manual backup/restore UI
  - [ ] Export groups to portable format
  - [ ] Import from other desktop organizers

### **Documentation & Help** 📚
- [ ] **In-App Help**
  - [ ] Interactive tutorial on first launch
  - [ ] Context-sensitive help tooltips
  - [ ] Video tutorials
  - [ ] FAQ section
  
- [ ] **User Documentation**
  - [ ] User guide (PDF/web)
  - [ ] Keyboard shortcut cheat sheet
  - [ ] Troubleshooting guide
  - [ ] Best practices guide

---

## 🔧 TECHNICAL DEBT

### **Code Cleanup**
- [x] ~~Remove excessive debug logs~~ ✓ **DONE & VERIFIED IN CURRENT SESSION**
- [ ] Remove or document test menu items ("Force All Visible", "Test Window")
- [ ] Standardize naming conventions across ViewModels
- [ ] Extract magic numbers to constants
- [ ] Reduce cyclomatic complexity in GroupWidget.xaml.cs

### **Architecture**
- [ ] Refactor Win32Interop into service pattern
- [ ] Separate UI logic from ViewModels (MVVM purity)
- [ ] Implement proper Dependency Injection
- [ ] Create abstraction layer for file system operations
- [ ] Centralize event aggregation

### **Testing**
- [ ] Unit tests for ViewModels
- [ ] Integration tests for persistence
- [ ] UI automation tests
- [ ] Performance benchmarks
- [ ] Memory leak detection

### **Build & Deployment**
- [ ] Automated versioning
- [ ] CI/CD pipeline (GitHub Actions)
- [ ] Automated installer builds
- [ ] Code signing certificate
- [ ] Update checker/auto-updater

---

## 🐛 KNOWN ISSUES

### **Critical**
- None currently 🎉

### **High Priority**
- None currently 🎉

### **Medium Priority**
- [ ] Window flicker during rapid view mode switches (minimize → hover → minimize)
- [ ] Drag threshold can be too sensitive on high-DPI displays
- [ ] Occasional position desync after monitor disconnect/reconnect

### **Low Priority**
- [ ] Minor visual glitches in fullscreen mode on secondary monitors
- ~~[ ] Settings window doesn't remember size/position~~ ✓ **FIXED IN BATCH 3**
- [ ] Hover timer can fire even when mouse leaves during drag

---

## 📊 SESSION HISTORY

### **Sizing Bug Fix Session** ✅ **[AWAITING USER SIGN-OFF]** — commit `a6b3c25`
- ✅ Fixed MinHeight coercion bug (root cause of bugs #2, #4, #6, #7)
  - MinHeight now set BEFORE Width/Height in all 6 view cases in UpdateViewMode() + ApplyViewMode()
- ✅ Fixed Hover + Compact footer gap (bugs #3, #5)
  - Replaced Measure(infinity) + Math.Ceiling(DesiredSize) with SizeToContent.WidthAndHeight + ActualWidth/Height
- ✅ Fixed Settings window ✕ close button vertical alignment
  - Added Margin="0,-16,0,-16" to cancel header padding; CornerRadius 9→10 to match window border
- ✅ Build: 0 errors, 0 warnings

### **v1.0 Release Prep Session** ✅ **[RELEASE READY - AWAITING USER SIGN-OFF]**
- ✅ Fixed NaN JSON serialization in PersistenceService → 45/45 tests pass
- ✅ Fixed CS8602 nullable dereference warning in GroupWidget.xaml.cs
- ✅ Filled LICENSE.txt placeholders (2025 Johan Fourie)
- ✅ Updated README: .NET 6 → .NET 10, correct clone URL
- ✅ Replaced placeholder contact email with GitHub Issues link
- ✅ Footer polish across all 6 view modes (opacity, separator, font, cursor, padding)
- ✅ Removed White/Gray/Black from color picker (rendering issues)
- ✅ Build: 0 errors, 0 warnings (Release config)
- ✅ All 45 unit tests passing

### **BATCH 3 (Previous)** ✅ **[MAJOR UI POLISH COMPLETE]**
- ✅ Window Resize for Normal/Grid modes ✓ VERIFIED
- ✅ Compact View Mode (2x3 grid, fixed size) ✓ VERIFIED
- ✅ Enum reordering (Compact=5 at end to avoid breaking saved data) ✓
- ✅ Size persistence for resizable modes ✓
- ✅ OnSizeChanged handler for resize tracking ✓
- ✅ Compact view footer visibility fix ✓
- ✅ Settings Window Always On Top ✓ VERIFIED
- ✅ All Dialogs Always On Top (Ungrouped Icons, Color Picker, Input Dialog) ✓ VERIFIED
- ✅ Icon Hover Effect (IconTile integration, accent color binding, 30% opacity) ✓ VERIFIED
- ✅ Layout Orientation Support (Horizontal for Normal/Hover, Vertical for Grid/Compact/Fullscreen) ✓ VERIFIED
- ✅ Transparency Slider Enhancements (tick marks, mouse wheel support) ✓ VERIFIED
- ✅ Settings Window Layout Polish (separators, spacing, position persistence) ✓ VERIFIED

### **BATCH 2 (Previous)** ✓ **VERIFIED**
- ✅ Settings Window Theme Support ✓
- ✅ Group Transparency Controls ✓
- ✅ Extended Color Palette (White, Gray, Black) ✓
- ✅ Sticky/Float Dock Mode ✓
- ✅ Show/Hide All with Sticky Support ✓
- ✅ Position Corruption Fixes ✓
- ✅ Debug Tools Enhanced ✓
- ✅ Status Footer in All Views ✓
- ✅ **BUG FIX:** Sticky mode restoration after Show All ✓
- ✅ **CLEANUP:** Verbose debug logging removed ✓

### **BATCH 1** (Previous)
- (Details from previous sessions)

---

## 🎯 RELEASE STRATEGY

### **PHASE 1: MVP RELEASE (Field Test)** 🚀 **← CURRENT TARGET**
**Goal:** Production-ready v1.0 for public release and user feedback collection  
**Timeline:** ~30 hours / 6-8 sessions  
**Target Date:** 2-3 weeks  

#### **Release Criteria (Must Have):**
- [x] All BATCH 2 features verified ✓
- [x] UI Polish completed ✓ (footer, color picker, settings window)
- [x] All critical bugs fixed ✓ (NaN serialization, CS8602 warning, hover jump)
- [x] Settings window polished ✓ (tabbed UI, About, Help, F1, position persistence)
- [x] User documentation ✓ (README updated: .NET 10, correct clone URL, features)
- [x] LICENSE.txt filled in ✓ (2025 Johan Fourie)
- [x] All 45 unit tests passing ✓
- [ ] Installer tested on clean Windows 10/11 *(post user sign-off)*
- [ ] No memory leaks in 24-hour test *(post user sign-off)*
- [ ] Test menu items removed or hidden *(deferred to v1.1)*

#### **Phase 1 Deliverables:**
**Core Features (Already Done ✓):**
- ✅ 6 View Modes (Minimized, Hover, Compact, Normal, Grid, Fullscreen) ✓ NEW!
- ✅ Window resize for Normal/Grid modes ✓ NEW!
- ✅ Sticky/Float dock modes
- ✅ Drag & drop shortcuts
- ✅ Custom group colors & icons
- ✅ Group transparency controls
- ✅ Light/Dark/System themes
- ✅ Multi-monitor support
- ✅ Snap-to-grid & pin position
- ✅ Auto-save with backups
- ✅ Usage tracking (top 10 most-used)

**Polish Tasks (In Progress):**
- [x] Footer styling refinement ✓ **DONE** (all 6 view modes polished)
- [x] Transparency UX improvements (tick marks + mouse wheel) ✓ **DONE**
- [x] Settings window organization (separators + spacing) ✓ **DONE**
- [ ] Color picker tooltips (optional enhancement)
- [x] Settings persistence (window size/position) ✓ **DONE**
- [x] Icon hover effect (integrated into all views) ✓ **DONE**
- [x] All dialogs topmost ✓ **DONE**

**Quality Gates:**
- [ ] Build successful on clean machine
- [ ] No crashes during normal usage (8-hour test)
- [ ] No data corruption (stress test)
- [ ] Performance acceptable (<50MB RAM, <1% CPU idle)
- [ ] All features documented in README

**Release Blockers (Must Fix Before Release):**
- [ ] Any critical bugs discovered during final testing
- [ ] Installer issues
- [ ] Data loss scenarios

---

### **PHASE 2: POWER USER EDITION** 💪
**Goal:** Enhanced features based on Phase 1 user feedback  
**Timeline:** ~60 hours / 12-15 sessions  
**Start After:** Phase 1 release + 2-4 weeks of user feedback  

#### **Features (Priority based on user feedback):**
- [ ] **Keyboard Shortcuts** (global hotkeys, navigation)
- [ ] **Grid View Enhancements** (customizable columns, icon sizes)
- [ ] **Hover Behavior Refinement** (configurable delays, auto-collapse options)
- [ ] **Drag & Drop Enhancements** (reorder, between groups)
- [ ] **Layout Presets** (save/load configurations)
- [ ] **Enhanced Usage Analytics** (dashboard, reports)
- [ ] **Shortcut count badge** in minimized view
- [ ] **Quick switcher** (Ctrl+Tab between groups)

**User Feedback Will Guide:**
- Which keyboard shortcuts are most needed
- Grid view preferences (columns, sizes)
- Layout preset use cases
- Analytics value proposition

---

### **PHASE 3: SMART & ENTERPRISE** 🧠
**Goal:** Advanced features for power users and teams  
**Timeline:** ~80+ hours / 16-20 sessions  
**Start After:** Phase 2 release + user feedback analysis  

#### **Features (Data-Driven Priorities):**
- [ ] **Cloud Sync** (OneDrive/Dropbox based on demand)
- [ ] **Auto-Organization** (AI grouping suggestions)
- [ ] **Context-Aware Behavior** (time-based, app-triggered)
- [ ] **Platform Integration** (Windows 11 Widgets, Search)
- [ ] **Third-Party Apps** (Steam, Epic, browsers)
- [ ] **Accessibility** (screen readers, high contrast)
- [ ] **Team Features** (shared layouts, sync)

**Long-Term Vision:**
- [ ] Mobile companion app (view/launch remotely)
- [ ] Web dashboard (analytics, remote management)
- [ ] Plugin system (community extensions)

---

## 📋 IMMEDIATE NEXT STEPS (PHASE 1 COMPLETION)

### **Session 1-2: UI Polish** ✨
- [ ] Footer styling (all view modes) - **AWAITING USER REQUIREMENTS**
- [ ] Add About dialog (version + contact info)
- [ ] Add Help dialog with F1 shortcut (how to use guide)
- ~~[ ] Transparency preset buttons~~ - **SCRAPPED**
- [x] Settings window layout & grouping ✓ **DONE**
- ~~[ ] Color picker tooltips~~ - **DEFERRED to after footer work**
- [x] Test in both themes ✓ **DONE**

### **Session 3-4: Quality & Testing** 🧪
- [ ] Remove/hide test menu items
- [ ] Settings window size/position persistence
- [ ] 24-hour memory leak test
- [ ] Multi-monitor stress test
- [ ] Clean install test (Windows 10 & 11)

### **Session 5-6: Documentation & Release Prep** 📚
- [ ] Update README with all features
- [ ] Create quick start guide
- [ ] Add tooltips to all settings
- [ ] Record demo video (optional)
- [ ] Create release notes
- [ ] Tag v1.0.0-rc1 (release candidate)

### **Session 7-8: Release** 🎉
- [ ] Final testing pass
- [ ] Create installer package
- [ ] GitHub release with binaries
- [ ] Publish release announcement
- [ ] Set up feedback channels (GitHub Issues/Discussions)
- [ ] Monitor for critical bugs (hotfix readiness)

---

## 🎯 QUICK WINS FOR PHASE 1 (Low Effort, High Impact)

**Can be done in 1-2 hours each:**
- [ ] **About Dialog** - Version info + contact us (NEXT UP)
- [ ] **Help Dialog (F1)** - How to use guide + keyboard shortcuts (NEXT UP)
- [ ] Remove test menu items
- ~~[ ] Preset opacity buttons (25%, 50%, 75%, 100%)~~ **SCRAPPED - Keeping slider as-is**
- ~~[ ] Settings window position persistence~~ ✓ **DONE**
- ~~[ ] Tooltips for color picker colors~~ **DEFERRED - After footer + color picker overall work**

---

**END OF BACKLOG** 📋

**Next Session:** User final test → v1.0.0 tag → GitHub Release 🚀
