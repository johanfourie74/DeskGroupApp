# DeskGroup - Development Backlog

**Last Updated:** 2025-01-09 (Settings Window - Tabbed Interface with About & Help)  
**Status:** Ready for Next Session  
**Commit:** Added tabbed Settings window with About and Help sections

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

- [ ] **Footer Styling Polish** ⏳ **AWAITING REQUIREMENTS**
  - User will provide specific requirements after break
  - Adjust font sizes for consistency across view modes
  - Refine background opacity/color for better visibility
  - Consider adding subtle separator between left/right text
  - Test readability in both light and dark themes

- [ ] **About Dialog** 🆕 **NEXT PRIORITY**
  - Display version info
  - Contact us information
  - Accessible from tray menu or Help menu

- [ ] **Help Dialog (F1)** 🆕 **NEXT PRIORITY**
  - How to use guide
  - Keyboard shortcut: F1
  - Accessible from tray menu or Help menu
  - Quick reference for all features

- [ ] **Color Picker Enhancement** ⏸️ **PAUSED - AFTER FOOTER CHANGES**
  - Verify new colors (White, Light Gray, Dark Gray, Black) work well in both themes
  - Overall color picker refinement to be addressed after footer polish
  - Test contrast/visibility with transparency settings

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

### **BATCH 3 (Current)** ✅ **[MAJOR UI POLISH COMPLETE]**
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
- [ ] UI Polish completed (current priority #1)
- [ ] All critical bugs fixed
- [ ] Settings window polished
- [ ] User documentation (README, basic guide)
- [ ] Installer tested on clean Windows 10/11
- [ ] No memory leaks in 24-hour test
- [ ] Test menu items removed or hidden

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
- [ ] Footer styling refinement (paused - awaiting detailed requirements)
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

**Next Session:** UI Polish (Footer + Transparency + Settings) 🎨
