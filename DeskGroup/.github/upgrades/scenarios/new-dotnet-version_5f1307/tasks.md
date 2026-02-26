# DeskGroup .NET 10 Upgrade Tasks

## Overview

This document tracks the upgrade of the `DeskGroup` solution to `.NET 10.0` (`net10.0-windows`) using a single atomic upgrade pass followed by validation and a final commit. Tasks cover prerequisites verification, the atomic project/package/compilation update, test execution, and the final commit.

**Progress**: 4/4 tasks complete (100%) ![100%](https://progress-bar.xyz/100)

---

## Tasks

### [✓] TASK-001: Verify prerequisites *(Completed: 2026-02-26 03:09)*
**References**: Plan §Phase 0

- [✓] (1) Verify required .NET 10 SDK is installed on the execution environment per Plan §Phase 0
- [✓] (2) Runtime/SDK version meets minimum requirements (**Verify**)
- [✓] (3) Inspect `global.json` or SDK version lock files for compatibility per Plan §Phase 0
- [✓] (4) Configuration files compatible with target SDK (**Verify**)

### [✓] TASK-002: Atomic framework and package upgrade with compilation fixes *(Completed: 2026-02-26 01:16)*
**References**: Plan §Migration Strategy, Plan §Project-by-Project Plans, Plan §Package Update Reference, Plan §Breaking Changes Catalog

- [✓] (1) Update `TargetFramework` to `net10.0-windows` in `src\DeskGroup\DeskGroup.csproj` per Plan §Project-by-Project Plans
- [✓] (2) Ensure `UseWPF` remains `true` and preserve `Nullable` / `ImplicitUsings` settings (**Verify**)
- [✓] (3) Update/verify package references per Plan §Package Update Reference (apply minimal compatible upgrades only if required)
- [✓] (4) Restore dependencies (dotnet restore) and ensure restore succeeds (**Verify**)
- [✓] (5) Build solution to identify compilation errors (dotnet build)
- [✓] (6) Fix all compilation errors found per Plan §Breaking Changes Catalog (address WPF binary-incompatible API usages)
- [✓] (7) Rebuild solution to verify fixes
- [✓] (8) Solution builds with 0 errors (**Verify**)

### [✓] TASK-003: Run test suite and validate upgrade *(Completed: 2026-02-26 04:12)*
**References**: Plan §Testing & Validation Strategy, Plan §Project-by-Project Plans

- [✓] (1) Run all test projects referenced in Plan §Testing & Validation Strategy (if present)
- [✓] (2) Fix any test failures (reference Plan §Breaking Changes Catalog for common remediation patterns)
- [✓] (3) Re-run tests after fixes
- [✓] (4) All tests pass with 0 failures (**Verify**)

### [✓] TASK-004: Final commit *(Completed: 2026-02-26 04:31)*
**References**: Plan §Source Control Strategy

- [✓] (1) Commit all remaining changes with message: "TASK-004: Complete upgrade to net10.0-windows"


