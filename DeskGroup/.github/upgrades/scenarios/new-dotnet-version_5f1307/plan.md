# .NET Version Upgrade Plan

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

---

## Executive Summary

Scenario: Upgrade solution to .NET 10.0 (net10.0-windows where applicable) using an All-At-Once Strategy.

High-level metrics (from assessment):
- Total Projects: 1 (all require upgrade)
- Project analyzed: `src\DeskGroup\DeskGroup.csproj`
- Current TFM: `net6.0-windows`
- Proposed TFM: `net10.0-windows`
- NuGet packages: 3 (all marked compatible in assessment)
- Total lines of code: 2,251
- APIs analyzed: 4,185
- Binary incompatible API hits: 274 (high impact)

Rationale for selected strategy: All-At-Once Strategy.
- Solution size: single project → fits All-At-Once conditions.
- Homogeneous codebase (WPF desktop app) with no inter-project dependencies.
- Assessment shows packages are compatible; primary work is code adjustments for binary-incompatible APIs.
- All-At-Once minimizes branching complexity and accomplishes upgrade in one atomic pass.

Key risks summarized:
- Binary-incompatible WPF-related APIs (approx. 274 occurrences) require code fixes.
- Potential runtime behavioral differences in WPF rendering and interop.

Primary objective:
- Update `DeskGroup.csproj` to target `net10.0-windows`, resolve compilation issues, ensure app builds cleanly, and validate functionality.

---

## Migration Strategy

Selected Strategy: All-At-Once Strategy — Atomic upgrade of the entire solution in a single coordinated operation.

Justification:
- Single project in solution → low complexity for atomic upgrade.
- Assessment suggests package compatibility; main effort is source adjustments.
- Team can focus on a single upgrade pass and then validate via build and tests.

Overview of atomic operations (single coordinated operation):
1. Update project file(s) to `TargetFramework` = `net10.0-windows`.
2. Update package references where assessment suggests versions (none required per assessment; verify latest compatible versions as part of upgrade).
3. Restore dependencies and build solution to reveal compilation errors.
4. Address compilation errors and API incompatibilities (code changes in one pass).
5. Rebuild and verify solution builds with 0 errors.
6. Execute automated tests (if any) and validate key scenarios.

Phases (conceptual only):
- Phase 0: Preparation (SDK/tooling validation)
- Phase 1: Atomic Upgrade (project TFM + package updates + build/fix)
- Phase 2: Validation (tests and runtime verification)

---

## Detailed Dependency Analysis

Summary:
- Single SDK-style WPF project: `src\DeskGroup\DeskGroup.csproj`.
- No project-to-project dependencies (leaf and root are the same project).
- No circular dependencies.

Implication: dependency ordering is trivial; All-At-Once applies to the single project.

Critical path:
- Modify project file → Build → Fix binary-incompatible API uses → Rebuild → Validate.

---

## Project-by-Project Plans

### Project: `src\DeskGroup\DeskGroup.csproj`

Current State:
- TFM: `net6.0-windows`
- Type: WPF desktop application (UseWPF=true)
- Packages (current):
  - `CommunityToolkit.Mvvm` 8.2.2
  - `Hardcodet.NotifyIcon.Wpf` 1.1.0
  - `Microsoft.WindowsAPICodePack-Shell` 1.1.0
- LOC: ~2,251
- Files with incidents: 16
- API binary-incompatibilities: ~274 hits

Target State:
- TFM: `net10.0-windows`
- Project builds and runs on .NET 10 runtime on Windows.

Migration Steps (atomic, must be performed as a single upgrade pass):
1. Preparation
   - Ensure .NET 10 SDK is installed on upgrade environment and CI (if applicable). Document SDK version to install.
   - If repository exists, create a single upgrade branch (feature branch). Note: assessment detected no Git repository.
2. Project file update
   - Update `TargetFramework` from `net6.0-windows` to `net10.0-windows` in `src\DeskGroup\DeskGroup.csproj`.
   - Verify `UseWPF` remains `true`, keep `Nullable` and `ImplicitUsings` settings.
3. Package verification
   - Confirm current package versions are compatible with .NET 10; if newer patch/minor versions exist that are compatible, include them in the atomic update. Assessment marks current packages as compatible; no mandatory updates required.
4. Build and compile
   - Restore packages and build to capture all compilation errors and diagnostics.
5. Address compilation issues
   - Resolve API changes flagged as binary-incompatible (see Breaking Changes catalog below). Make source changes to remove/replace incompatible usages.
6. Rebuild
   - Rebuild solution and ensure zero compilation errors.
7. Validation
   - Run available unit/integration tests (if present).
   - Perform runtime smoke checks (startup, key flows, tray icon, UI rendering).
8. Finalization
   - Update documentation about target runtime and supported Windows version if applicable.

Validation Checklist (per-project):
- [ ] `TargetFramework` updated to `net10.0-windows` in project file
- [ ] Project restores and builds with 0 errors
- [ ] No new package vulnerabilities introduced
- [ ] Runtime startup: main window and tray integration function
- [ ] Key features exercised manually or via automated tests

---

## Package Update Reference

Assessment indicates the three packages used are compatible with the proposed framework. No forced package upgrades suggested in the assessment report.

| Package | Current Version | Suggested Change | Notes |
|---|---:|---|---|
| `CommunityToolkit.Mvvm` | 8.2.2 | No change required (verify latest compatible minor/patch) | MVVM helper library; verify compatibility with .NET 10 but assessment marked compatible. |
| `Hardcodet.NotifyIcon.Wpf` | 1.1.0 | No change required (verify latest) | Legacy tray icon helper. Validate behavior on .NET 10 and Windows runtime. |
| `Microsoft.WindowsAPICodePack-Shell` | 1.1.0 | No change required (verify latest) | Windows API Code Pack; ensure compatibility with Windows APIs under .NET 10.

Recommendation: During atomic upgrade, attempt to restore with current versions. If compilation/runtime issues arise tied to packages, update to the minimal compatible version that resolves the issue and document it.

---

## Breaking Changes Catalog

Assessment highlights ~274 binary-incompatible API hits concentrated in WPF types. Focus areas to inspect and remediate:

High-impact API groups (examples from assessment):
- `System.Windows.Media.ImageSource` (image handling)
- `System.Windows.Rect` (geometry/rect usage)
- `System.Windows.Media.SolidColorBrush` and Color APIs
- `System.Windows.Visibility` and Window visibility handling
- `System.Windows.Point` and constructors
- `System.Windows.Interop.HwndSource` (interop)
- `System.Windows.Freezable` usages
- `System.Windows.Application` and `Application.Current` access
- `System.Windows.Window` lifecycle and constructors

Typical remediation patterns:
- Replace removed or changed constructors with advised factory or alternate constructors.
- Update usages of `Rect`/`Point` property accessors if signatures or behavior changed.
- For `Freezable`-based types, ensure correct freeze semantics and immutability where required.
- For interop (`HwndSource`), verify initialization code and any P/Invoke usage.

⚠️ For each compilation error discovered after the TFM update, capture the symbol, compiler message, and map to one of these categories. Create small, scoped code changes to restore compilation.

---

## Testing & Validation Strategy

Because this is an All-At-Once upgrade, testing occurs after the atomic upgrade pass completes.

Automated tests:
- Run unit tests and integration tests (if present). No test projects were reported in assessment; if present, add them to the validation step.

Manual/runtime checks (smoke tests):
- Application startup and shutdown
- Tray icon availability and behavior (Hardcodet.NotifyIcon.Wpf)
- Main window appearance and core UI flows (drag/drop, group operations)
- File system interactions and Shell API usages (Microsoft.WindowsAPICodePack-Shell)
- Common user flows that exercise WPF rendering (images, brushes, layout)

Validation checklist (solution-level):
- [ ] Solution builds with 0 errors
- [ ] Unit tests pass (if any)
- [ ] Key smoke test flows pass
- [ ] No critical exceptions in logs during startup and UI interactions

---

## Risk Management

Risk summary for `DeskGroup`:
- Risk level: Medium — single project, but substantial API incompatibilities (274 binary-incompatible hits).

Key risks and mitigations:
1. Binary-incompatible WPF APIs
   - Mitigation: Triage compiler errors, fix incrementally in a single pass, focus on high-frequency APIs first (`ImageSource`, `Rect`, `SolidColorBrush`).
2. Runtime behavioral regressions (UI rendering, interop)
   - Mitigation: Define smoke test checklist and exercise areas relying on system DPI, composition, and interop.
3. Package runtime incompatibility not caught by assessment
   - Mitigation: If runtime failures point to packages, update those packages to compatible versions and revalidate.

Contingency plans:
- If an unresolvable blocking issue is discovered, revert project file TFM change and isolate the specific change in a short-lived branch while keeping other remediation work minimal.

---

## Complexity & Effort Assessment

Per-project complexity (relative):
- `DeskGroup` — Complexity: Medium
  - Reasons: WPF usage with many binary-incompatible API hits; modest codebase (2,251 LOC); single project simplifies coordination.

Effort guidance (no time estimates):
- Expect source edits across view models, UI code, and interop points. Prioritize compiler-driven fixes.

---

## Source Control Strategy

Repository status: assessment reported no Git repository found. If a Git repo is present in your environment, follow this guidance:
- Create a single upgrade branch for the atomic change (e.g., `upgrade/dotnet-10`).
- Make a single cohesive commit that includes project file TFMs and package version updates.
- Keep compilation fixes in the same branch so the atomic upgrade can be validated end-to-end.
- Use PR with CI that runs restore/build/tests before merge.

If no Git repo is available, document changes and apply them atomically when executing upgrade in the target environment.

---

## Success Criteria

The migration is considered complete when:
1. All projects target their proposed framework (`net10.0-windows` for DeskGroup).
2. All package updates from assessment (none required) are applied or explicitly documented if changed.
3. Solution builds with 0 compilation errors.
4. Unit and integration tests pass (if present).
5. Key runtime smoke tests pass (startup, tray icon, UI flows).
6. No unresolved security vulnerabilities in NuGet packages remain.

---

## Notes & Next Steps

- Next operational step (Execution stage): implement the atomic changes described above (project file TFM update, package verification, build and fix). As a Planner Agent I cannot perform these changes; this plan is the blueprint for an executor.
- If you want, I can convert this plan to a single TASK-001 atomic upgrade task (for an executor) following All-At-Once batching rules.

*Plan generated from assessment `C:\Dev\DeskGroup\.github\upgrades\scenarios\new-dotnet-version_5f1307\assessment.md`.*
