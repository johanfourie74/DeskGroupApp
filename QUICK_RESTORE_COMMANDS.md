# Quick Restore Commands

## To Create Git Commit NOW (Recommended)

```bash
git add .
git commit -m "Phase 1 Complete: Hover View Working - Restore Point"
git tag phase1-complete
```

## To Restore to This Point Later

```bash
# Option 1: If you tagged it
git reset --hard phase1-complete

# Option 2: If you just committed
git log --oneline -10  # Find the commit hash
git reset --hard <commit-hash>

# Clean up
git clean -fd
```

## Emergency Restore (No Git)

```powershell
# 1. Stop app
Stop-Process -Name DeskGroup -Force -ErrorAction SilentlyContinue

# 2. Delete corrupted data
Remove-Item -Recurse -Force "$env:APPDATA\DeskGroup"

# 3. Rebuild
cd C:\Dev\DeskGroup
dotnet clean
dotnet build

# 4. Test
cd src\DeskGroup\bin\Debug\net10.0-windows
.\DeskGroup.exe
```

---

**Current files that work:**
- ✅ src\DeskGroup\Views\GroupWidget.xaml (3 inline views)
- ✅ src\DeskGroup\Views\GroupWidget.xaml.cs (UpdateViewMode with guards)
- ✅ src\DeskGroup\App.xaml.cs (position-based visibility)
- ✅ src\DeskGroup\ViewModels\MainViewModel.cs (staggered positioning)
- ✅ src\DeskGroup\Controls\DragThumb.xaml.cs (throttled dragging)
