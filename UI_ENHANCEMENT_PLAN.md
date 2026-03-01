# DeskGroup UI Enhancement Plan

**Status:** Ready to Execute  
**Date:** 2026-03-01  
**Branch:** Dev  

---

## 🎯 EXECUTION STRATEGY

Execute in **5 BATCHES** - commit after each batch to ensure clean rollback points.

---

## BATCH 1: Quick Wins (Visual Polish) ⚡
**Estimated Time:** ~15 minutes  
**Risk Level:** LOW  

### #5: Rounded Corners for All Windows
- Apply `CornerRadius="10"` to SettingsWindow and UngroupedIconsDialog
- Match GroupWidget's existing rounded corner style
- **Files to modify:**
  - `src/DeskGroup/Views/SettingsWindow.xaml`
  - `src/DeskGroup/Views/UngroupedIconsDialog.xaml`

### #8: Fix Tray Icon Hanging on Exit
- Current issue: Icon stays visible after exit until mouse hover
- **Solution:** Add `Shell_NotifyIcon` direct call to force immediate removal
- Use P/Invoke to call Windows API directly
- **Files to modify:**
  - `src/DeskGroup/App.xaml.cs` (OnExit method)

### #10: Better Balloon Notifications
- Add context-aware tips:
  - On first group creation: "Tip: Right-click groups to customize!"
  - On desktop icon detection: "Found X ungrouped icons - organize them?"
  - On hotkey trigger: Show which hotkey was used
  - On theme change: "Theme switched to [mode]"
- **Files to modify:**
  - `src/DeskGroup/App.xaml.cs` (various event handlers)
  - `src/DeskGroup/ViewModels/MainViewModel.cs` (add notification helpers)

---

## BATCH 2: Settings & Customization 🎨
**Estimated Time:** ~20 minutes  
**Risk Level:** MEDIUM  

### #1: Theme Selection in Settings
- Add dropdown: "Default" (current custom) | "Light" | "Dark" | "Follow System"
- "Default" = current behavior (custom colors)
- "Light/Dark/System" = Standard Windows 11 theme
- **Files to modify:**
  - `src/DeskGroup/Models/AppSettings.cs` (add `WindowThemeStyle` enum)
  - `src/DeskGroup/ViewModels/SettingsViewModel.cs` (add property)
  - `src/DeskGroup/Views/SettingsWindow.xaml` (add ComboBox)
  - `src/DeskGroup/App.xaml.cs` (modify `ApplyTheme` logic)
  - Create new theme files: `Themes/StandardLightTheme.xaml`, `Themes/StandardDarkTheme.xaml`

### #2: Transparency Slider
- Add slider in Settings: 0% (opaque) to 80% (very transparent)
- Default: 0% (current behavior)
- Apply to all group windows
- **Files to modify:**
  - `src/DeskGroup/Models/AppSettings.cs` (add `GroupBackgroundOpacity` property, default 1.0)
  - `src/DeskGroup/ViewModels/SettingsViewModel.cs` (add property)
  - `src/DeskGroup/Views/SettingsWindow.xaml` (add Slider)
  - `src/DeskGroup/Views/GroupWidget.xaml` (bind Border Opacity)

---

## BATCH 3: Bug Fixes 🐛
**Estimated Time:** ~15 minutes  
**Risk Level:** MEDIUM  

### #6: Fix Dock Mode Not Saving Per-Group
**Current Bug:**
- Dock mode (Float/Sticky) changes in individual group settings don't save
- Default setting in Settings works for new groups only

**Root Cause Analysis:**
- Check if `GroupViewModel.DockMode` setter calls save
- Check if property change notification is firing
- Check if `ApplySettingsChanges` is overwriting individual settings

**Solution:**
- Ensure `GroupViewModel.DockMode` setter:
  1. Updates Model
  2. Calls `NotifyPropertyChanged`
  3. Triggers save
- Ensure Settings changes ONLY affect new groups, not existing ones
- **Files to check/modify:**
  - `src/DeskGroup/ViewModels/GroupViewModel.cs`
  - `src/DeskGroup/ViewModels/MainViewModel.cs` (ApplySettingsChanges)

### #9: Drag to Resize + Remember Size
- Enable window resizing in Normal/Grid modes
- Save Width/Height on resize end
- Restore on next startup
- **Files to modify:**
  - `src/DeskGroup/Views/GroupWidget.xaml` (remove fixed ResizeMode)
  - `src/DeskGroup/Views/GroupWidget.xaml.cs` (add SizeChanged handler)
  - `src/DeskGroup/ViewModels/GroupViewModel.cs` (already has Width/Height, ensure save)

---

## BATCH 4: Icon Context Menu & Reordering 🖱️
**Estimated Time:** ~25 minutes  
**Risk Level:** HIGH  

### #4: Right-Click Context Menu on Icons
- Right-click on any icon/shortcut:
  - "Remove from Group"
  - "Move to..." → Submenu with all other groups
- **Implementation:**
  - Add `ContextMenu` to IconButton in GroupWidget
  - Wire up Remove and Move commands
- **Files to modify:**
  - `src/DeskGroup/Views/GroupWidget.xaml` (add ContextMenu to icon template)
  - `src/DeskGroup/ViewModels/GroupViewModel.cs` (add RemoveIcon, MoveIcon commands)

### #3: Drag and Reorder Icons
- Drag within same group: Reorder
- Drag to another group: Move
- Drag outside: Remove from group (delete shortcut file)
- **Implementation:**
  - Use WPF Drag/Drop with `DragDrop.DoDragDrop`
  - Detect drop target (same group, other group, desktop)
  - Update icon order in Model
- **Files to modify:**
  - `src/DeskGroup/Views/GroupWidget.xaml` (add drag/drop handlers)
  - `src/DeskGroup/Views/GroupWidget.xaml.cs` (implement drag logic)
  - `src/DeskGroup/ViewModels/GroupViewModel.cs` (add ReorderIcons, MoveIcon methods)
  - `src/DeskGroup/Models/IconGroup.cs` (ensure icon order is persisted)

---

## BATCH 5: Advanced Tray Menu 🚀
**Estimated Time:** ~20 minutes  
**Risk Level:** HIGH  

### #7: Left-Click Tray → Show Group List
- Left-click or hover on tray icon shows popup
- Popup displays:
  - Scrollable list of groups (if many)
  - Each item: Group icon + Group name
  - Click a group:
    - If Sticky: Temporarily switch to Float
    - Show window on top
    - Auto-hide when user clicks elsewhere
- **Implementation:**
  - Create new `QuickAccessPopup` window
  - Position near tray icon (calculate from cursor)
  - Use `Popup` or `Window` with `Topmost=true`
  - Wire to `TrayIcon.TrayLeftMouseDown`
- **Files to create:**
  - `src/DeskGroup/Views/QuickAccessPopup.xaml`
  - `src/DeskGroup/Views/QuickAccessPopup.xaml.cs`
- **Files to modify:**
  - `src/DeskGroup/App.xaml.cs` (wire left-click event)
  - `src/DeskGroup/ViewModels/GroupViewModel.cs` (add temporary float mode)

---

## ✅ SUCCESS CRITERIA

After each batch:
1. ✅ Code compiles with no errors
2. ✅ Manual testing confirms functionality works
3. ✅ Git commit with descriptive message
4. ✅ User confirmation before next batch

---

## 📝 NOTES

- **Icon fix is DONE:** Tray icon full-size, app icon embedded
- **Current status:** All icons working perfectly!
- **Testing environment:** Windows 11, 125% DPI scaling
- **Current branch:** Dev (up to date with origin)

---

## 🚀 READY TO START: BATCH 1

**First commits:**
- "Added rounded corners to Settings and Dialogs"
- "Fixed tray icon hanging on exit with Shell_NotifyIcon API"
- "Enhanced balloon notifications with context-aware tips"

---

**Total Estimated Time:** ~95 minutes (1.5 hours)  
**Approach:** Incremental, test-driven, commit after each batch  
**Risk Mitigation:** Can rollback any batch without affecting others
