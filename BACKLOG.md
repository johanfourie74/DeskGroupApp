# DeskGroup - Development Backlog

**Last Updated:** 2026-03-31 (session 11)
**Current release:** v1.4.5 (live)
**Pending release:** —

---

## 🔜 DO NEXT (in order)

*(no immediate release pending — v1.4.5 just shipped)*

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
