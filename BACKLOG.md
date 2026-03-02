# DeskGroup - Development Backlog

**Last Updated:** After BATCH 2 Session + Debug Cleanup  
**Status:** Ready for Next Session  
**Commit:** Updated with each commit  

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
- [ ] **Footer Styling Polish**
  - Adjust font sizes for consistency across view modes
  - Refine background opacity/color for better visibility
  - Consider adding subtle separator between left/right text
  - Test readability in both light and dark themes
  
- [ ] **Transparency Control UX**
  - Add visual preview of opacity changes
  - Consider adding preset buttons (25%, 50%, 75%, 100%)
  - Add tooltip showing current percentage value
  
- [ ] **Color Picker Enhancement**
  - Verify new colors (White, Light Gray, Dark Gray, Black) work well in both themes
  - Consider adding color name tooltips
  - Test contrast/visibility with transparency settings
  
- [ ] **Settings Window Polish**
  - Ensure consistent spacing and alignment
  - Add descriptive labels/tooltips for all settings
  - Consider grouping related settings (Appearance, Behavior, etc.)

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
- [ ] Settings window doesn't remember size/position
- [ ] Hover timer can fire even when mouse leaves during drag

---

## 📊 SESSION HISTORY

### **BATCH 2 (Current)** ✓ **VERIFIED**
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

## 🎯 NEXT SESSION SUGGESTIONS

### **Immediate Next Steps:**
1. ✅ **UI Polish** (Item #1 above) - High visibility, improves user experience
2. **Remove Test Menu Items** (if no longer needed)
3. **Keyboard Shortcuts** - High user demand feature
4. **Grid View Enhancements** - Natural extension of existing view modes

### **Quick Wins (Low Effort, High Impact):**
- [ ] Configurable hover delay
- [ ] Shortcut count badge in minimized view
- [ ] Settings window position/size persistence
- [ ] Preset opacity buttons (25%, 50%, 75%, 100%)

### **Long-Term Goals:**
- [ ] Cloud sync (high complexity, high value)
- [ ] AI-powered grouping (research phase)
- [ ] Windows 11 Widgets integration (platform-specific)

---

**END OF BACKLOG** 📋
