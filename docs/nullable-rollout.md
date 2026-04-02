# Nullable Reference Types Rollout Policy

This document describes the strategy, implementation, and operational procedures for escalating nullable reference type warnings to errors in the LGYM API codebase.

## Objective

Ensure high-quality, predictable code by enforcing strict nullable safety in production modules while allowing for a phased, non-disruptive rollout across the solution.

## Current State (Phase 1)

As of Phase 1, the following projects are **Gated** (warnings-as-errors for CS8600-CS8699):

- `LgymApi.Domain`
- `LgymApi.BackgroundWorker.Common`

The following production projects are **Non-Gated** (warnings are visible but non-blocking) and await future phases:

- `LgymApi.Api`
- `LgymApi.Application`
- `LgymApi.Infrastructure`
- `LgymApi.BackgroundWorker`

Test projects are intentionally excluded from the strict nullable gate.

## Exclusion Strategy

### 1. Generated Code (EF Migrations)
EF Core migrations automatically emit `#nullable disable` in generated `Designer.cs` and `ModelSnapshot.cs` files. The C# compiler respects these directives even in gated projects, so no manual exclusion is required for migration artifacts.

### 2. Test Projects
Test projects (`*Tests`, `*TestUtils`) are excluded from the gate to allow for intentional null-passing in boundary and negative tests. These projects are identified via the `IsTestProject` property in `Directory.Build.props`.

### 3. Third-party or Legacy Code
If a specific file in a gated project cannot reasonably be fixed, use `#nullable disable` at the file level as a targeted exception.

## Expansion Procedure

To add a new project to the gated set:

1. **Fix existing violations**: Ensure the target project builds with zero nullable warnings (`CS8600-CS8699`).
2. **Update Policy**: Open `Directory.Build.props` and add the project name to the `IsGatedProductionProject` property condition.
3. **Update CI Gate**: Open `.github/workflows/pr-and-main-tests.yml` and add a new `dotnet build` line for the project in the "Verify nullable warnings gate" step.
4. **Verify**: Run a full solution build locally and ensure CI passes.

## Exception & Rollback Strategy

### Emergency Bypasses
If a critical hotfix is blocked by a nullable warning in a gated project and cannot be immediately fixed safely:
1. Use `#nullable disable` in the affected file to bypass the gate for that specific file.
2. If multiple files are affected, temporarily remove the project from `IsGatedProductionProject` in `Directory.Build.props`.
3. **Mandatory**: Document the reason for the bypass and create a follow-up task to restore the gate.

### CI Failure Recovery
If the "Verify nullable warnings gate" step fails in CI:
- Identify the project and the CS86xx warning in the logs.
- Reproduce locally by building only that project: `dotnet build src/PathToProject/Project.csproj --configuration Release`.
- Fix the violation or use a targeted `#nullable disable` if appropriate.

## Local Verification

To verify the gate locally, run:

```bash
# Build a specific gated project with strict enforcement
dotnet build LgymApi.Domain/LgymApi.Domain.csproj --configuration Release --no-restore

# Build the entire solution (will show warnings for non-gated projects)
dotnet build LgymApi.sln --configuration Release --no-restore
```
