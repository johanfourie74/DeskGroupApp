## Moving to Planning Stage

The assessment has identified what needs to change for your .NET 10 Upgrade. Now I'll create a detailed specification explaining *how* to make those changes.

### What I'll Create

A comprehensive `plan.md` that transforms assessment findings into an actionable roadmap:

- **Migration Strategy** — The approach best suited to your codebase (incremental phases or coordinated update)
- **Execution Sequence** — Dependency-ordered steps ensuring nothing breaks along the way
- **Risk Mitigation** — Strategies to handle potential issues before they occur
- **Validation Checkpoints** — Clear success criteria for each phase

This plan becomes the blueprint for the Execution stage, where actual changes will be applied systematically.

Let's build your migration plan!


# Migration Plan: Upgrade to .NET 10.0 (net10.0-windows)

> Note: This document was generated from the assessment at `C:\Dev\DeskGroup\.github\upgrades\scenarios\new-dotnet-version_02d337\assessment.md` and follows the All-At-Once strategy.


## Table of Contents

- [Executive Summary](#executive-summary)
- [Migration Strategy](#migration-strategy)
- [Detailed Dependency Analysis](#detailed-dependency-analysis)
- [Project-by-Project Plans](#project-by-project-plans)
- [Package Update Reference](#package-update-reference)
- [Breaking Changes Catalog](#breaking-changes-catalog)
- [Testing & Validation Strategy](#testing--validation-strategy)
- [Risk Management](#risk-management)
- [Complexity & Effort Assessment](#complexity--effort-assessment)
- [Source Control Strategy](#source-control-strategy)
- [Success Criteria](#success-criteria)


## Executive Summary

### Selected Strategy
**All-At-Once Strategy** — All projects upgraded simultaneously in a single atomic operation.

**Rationale**:
- Solution size: 1 project (small)
- Current target (per repo): `net8.0-windows` (already moved from `net6.0` earlier)
- Assessment shows broad API incompatibilities but only a single project surface to update
- Team can accept short, coordinated instability window

### High-level Metrics (from assessment)
- Total Projects: 1 (all require upgrade)
- Current project target: `net8.0-windows`
- Proposed target: `net10.0-windows`
- Total NuGet packages: 3 (2 compatible, 1 incompatible)
- Total LOC: 4,257
- Estimated LOC to modify: 1,740+ (≈40.9%) — primarily API compatibility fixes in WPF code paths

### Critical Issues
- Large number of binary-incompatible APIs (~1,694) identified by assessment; expect compile-time errors requiring code changes
- `Hardcodet.NotifyIcon.Wpf` flagged as incompatible by assessment
- `Microsoft.WindowsAPICodePack-*` packages target .NET Framework and will raise NU1701 compatibility warnings


## Migration Strategy

- Apply an atomic, all-project upgrade to `net10.0-windows` (single coordinated change across the repo).
- Rationale: single project, simpler coordination, aligns with All-At-Once guidance in scenario instructions.

Phases (for human understanding):
- Phase 0: Preparation (tooling, branch, backups)
- Phase 1: Atomic Upgrade (project TFM change + package updates)
- Phase 2: Build & Fix Pass (compile, address API incompatibilities)
- Phase 3: Comprehensive Testing (unit + integration + smoke)

Note: Although described as phases, the atomic upgrade (Phase 1) is executed as a single task: update project TFM, update packages, restore, build and fix compilation errors in one bounded pass.


## Detailed Dependency Analysis

- Single SDK-style WPF project (`src/DeskGroup/DeskGroup.csproj`). No project-to-project dependencies.
- No circular dependencies.
- Dependency surface: 3 NuGet packages (WPF, Windows API related, MVVM toolkit).

Conclusion: All-At-Once is appropriate; dependency ordering minimal.


## Project-by-Project Plans

### Project: `src/DeskGroup/DeskGroup.csproj`

**Current State**:
- TargetFramework: `net8.0-windows` (current repository state)
- SDK-style: True
- Type: WPF desktop application
- Packages currently referenced (from assessment / csproj):
  - `CommunityToolkit.Mvvm` — current (assessment): `8.4.0` (or 8.x series in repo)
  - `Hardcodet.NotifyIcon.Wpf` — assessment flags as incompatible
  - `Microsoft.WindowsAPICodePack-Shell` / `Core` — `1.1.0` (targets .NET Framework)

**Target State**:
- TargetFramework: `net10.0-windows`
- All package references updated to versions compatible with `net10.0` where possible

**Migration Steps (atomic)**:
1. Preparation: create upgrade branch `upgrade/net10` from current working state and ensure no pending changes (commit/stash as per repo policy).
2. Update project file (single atomic edit): set `<TargetFramework>net10.0-windows</TargetFramework>` for the project and ensure `<UseWPF>true</UseWPF>` remains.
3. Update package references according to [Package Update Reference] below.
4. Restore packages: `dotnet restore`.
5. Build solution: `dotnet build -c Release` to collect compile errors introduced by TFM and package updates.
6. Fix compile-time issues (API incompatibilities) across codebase in one pass.
7. Rebuild and iterate until solution builds with 0 errors.
8. Run tests (if any) and perform validation checklist.

**Package/Dependency Updates (see Package Update Reference)**
- Apply the explicit package version changes listed in the package reference table below.

**Expected Breaking Changes**
- Numerous WPF API binary incompatibilities; expect to update usages around routed events, dependency properties, control types and constructors.
- Tray/icon related code that uses `Hardcodet.NotifyIcon.Wpf` or Windows API Code Pack might require rework or replacement if incompatibilities surface at runtime.

**Validation Checklist**
- [ ] Project file updated to `net10.0-windows` and committed to `upgrade/net10` branch
- [ ] `dotnet restore` completes with no vulnerabilities reported
- [ ] Solution builds with 0 errors
- [ ] Unit tests (if present) pass
- [ ] Smoke test: application launches and main UI appears


## Package Update Reference

Summary of package changes to apply atomically (exact versions chosen from assessment and NuGet where available):

- `CommunityToolkit.Mvvm`
  - Current: `8.2.2` or `8.4.0` (assessment shows 8.4.0)
  - Target: `8.4.0` (use latest 8.x compatible with WPF)
  - Reason: compatibility and bugfixes; no breaking changes expected within 8.x for typical MVVM usage

- `Hardcodet.NotifyIcon.Wpf`
  - Current: `1.1.0` (repo) / assessment flagged incompatible
  - Target: evaluate available versions; if no net10-compatible release exists, consider:
    - Keep for now and validate runtime behavior (may work despite flagged incompatibilities)
    - Or replace with a maintained alternative (e.g., `Hardcodet.NotifyIcon.Wpf` fork that targets modern .NET, or implement tray integration using `Windows.Win32`/`CsWin32`)
  - Reason: assessment indicates incompatibility; requires runtime verification or replacement

- `Microsoft.WindowsAPICodePack-Shell` / `Core`
  - Current: `1.1.0` (targets .NET Framework)
  - Target: recommended replacement options:
    - Replace P/Invoke usage with `Windows.Win32` (CsWin32) or `Vanara.PInvoke.*` packages
    - If code-pack functionality is minimal, reimplement using supported APIs
  - Reason: these packages produce NU1701 warnings and are not built for modern .NET; keep only as a temporary compatibility shim if needed


## Breaking Changes Catalog

(High-level - concrete instances will be collected after the first build pass)

- WPF API surface changes: routed events, dependency property behaviours, control constructors and default styles.
- `System.Uri` constructor behavior changes (behavioral change entries in assessment).
- Any usage of APIs removed or changed between .NET 8 and .NET 10 that are surfaced as compile errors.

Action: After the atomic TFMs and package updates, run a full build to get a precise list of compile errors; update this catalog with file/line-level entries.


## Testing & Validation Strategy

- Phase 1 validation (post-upgrade build): solution builds with 0 errors.
- Phase 2 validation (unit): run `[dotnet test]` for test projects (none detected in assessment but run if present).
- Phase 3 validation (smoke): launch application on a developer workstation / CI environment and verify main window and key flows.

Automated checks to include in CI after patch applied:
1. `dotnet restore`
2. `dotnet build --configuration Release`
3. `dotnet list package --vulnerable`
4. If tests exist: `dotnet test`


## Risk Management

Key risks:
- High count of API incompatibilities will require developer time to fix compile errors.
- `Hardcodet.NotifyIcon.Wpf` and `Microsoft.WindowsAPICodePack-*` may not be supported on net10; replacement work may be needed.
- Runtime behavior differences even after compilation — require thorough smoke testing.

Mitigations:
- Keep an isolated feature branch `upgrade/net10` for atomic changes.
- Run CI with build + tests and quick smoke to detect runtime issues early.
- If `WindowsAPICodePack` causes blocking issues, plan a focused subtask to replace it with `CsWin32` or `Vanara`.


## Complexity & Effort Assessment

- Overall complexity: Medium — single project mitigates coordination overhead but many API fixes are necessary.
- Project complexity: High for WPF areas that use many API surface points noted in assessment.
- Risk rating per project: Medium-High.


## Source Control Strategy

- Create branch: `upgrade/net10` from current working commit.
- Single atomic commit for TFM + package updates.
- Subsequent commits to fix compile errors and tests are allowed in the same branch. Keep PR scope limited to the upgrade.

Suggested commit pattern:
- Commit 1: `chore: upgrade project target to net10.0-windows and update package references`
- Commit 2..N: fix compile errors and test failures (clearly separate logical fixes)


## Success Criteria

- All projects target `net10.0-windows`.
- All package updates from the Package Update Reference are applied or an approved replacement is in place.
- Solution builds with 0 errors.
- No high-severity vulnerable packages remain (CI `dotnet list package --vulnerable` passes).
- Key UI flows (app launch, main window, tray behavior) validated manually or via automated checks.


---

(End of plan skeleton and first-pass content.)


<!-- Iteration note: additional detailed per-file fix list and breaking-change mappings will be appended after the first build pass on the `upgrade/net10` branch. -->
