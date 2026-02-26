# DeskGroup .NET 10 Upgrade Tasks

## Overview

This document tracks the upgrade of the repository from `net8.0-windows` to `net10.0-windows`. Work will verify prerequisites, perform the atomic project and package upgrade, then run tests and vulnerability checks.

**Progress**: 3/3 tasks complete (100%) ![0%](https://progress-bar.xyz/100)

---

## Tasks

### [✓] TASK-001: Verify prerequisites *(Completed: 2026-02-26 16:22)*
**References**: Plan §Phase 0 (Preparation), Plan §Source Control Strategy

- [✓] (1) Verify required .NET SDK/runtime version installed per Plan §Phase 0
- [✓] (2) Runtime/SDK version meets minimum requirements (**Verify**)
- [✓] (3) Verify `global.json` (if present) is compatible with the required SDK per Plan §Phase 0 (**Verify**)
- [✓] (4) Verify required CLI tools and toolchain versions per Plan §Phase 0 (**Verify**)

### [✓] TASK-002: Atomic framework and package upgrade with compilation fixes *(Completed: 2026-02-26 17:25)*
**References**: Plan §Project-by-Project Plans, Plan §Package Update Reference, Plan §Breaking Changes Catalog, Plan §Source Control Strategy

- [✓] (1) Update target framework in `src/DeskGroup/DeskGroup.csproj` to `net10.0-windows` per Plan §Project-by-Project Plans
- [✓] (2) Update package references per Plan §Package Update Reference (notably `CommunityToolkit.Mvvm`, `Hardcodet.NotifyIcon.Wpf`, `Microsoft.WindowsAPICodePack-*`) 
- [✓] (3) Restore dependencies (`dotnet restore`) per Plan §Package Update Reference
- [✓] (4) Build solution and fix all compilation errors introduced by the TFM and package updates (address items in Plan §Breaking Changes Catalog)
- [✓] (5) Solution builds with 0 errors (**Verify**)
- [✓] (6) Commit changes with message: "TASK-002: upgrade project target to net10.0-windows and update package references"

### [✓] TASK-003: Run tests and validate upgrade *(Completed: 2026-02-26 15:27)*
**References**: Plan §Testing & Validation Strategy, Plan §Breaking Changes Catalog

- [✓] (1) Run test projects listed in Plan §Testing & Validation Strategy using `dotnet test`; if Plan §Testing & Validation Strategy lists no test projects, verify none are present (**Verify**)
- [✓] (2) Fix test failures identified in the run (apply fixes bounded to issues referenced in Plan §Breaking Changes Catalog)
- [✓] (3) Re-run tests once after applying fixes (`dotnet test`)
- [✓] (4) All tests pass with 0 failures (**Verify**)
- [✓] (5) Commit test fixes with message: "TASK-003: Complete testing and validation"

