# Assemblies

## Why

This area contains the shipped mod DLL and the automated tests that define Harmony patch behavior.

## What

- Main paths:
  - `QudJP.csproj` for the `net48` mod DLL
  - `QudJP.Tests/` for the `net10.0` test project
  - `src/Patches/` for Harmony patch classes
  - `src/` for translators, renderers, observability helpers, and shared utilities
- Source of truth:
  - patch behavior is defined by tests in `QudJP.Tests/`
  - layer boundaries live in `docs/test-architecture.md`
  - translation-route, ownership, runtime, and deployment rules live in `docs/RULES.md`

## How

- Build and test:

```bash
dotnet build Mods/QudJP/Assemblies/QudJP.csproj
dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj
dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter TestCategory=L1
dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter TestCategory=L2
dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter TestCategory=L2G
```

- Prefer producer-owned or stable mid-pipeline fixes. Many sink and near-sink routes are intentionally observation-only.
- Use `~/dev/coq-decompiled_stable/` to trace upstream producers, verify signatures, and investigate unclaimed routes.
- For C# patch, translator, observability, or target-method changes, use structural search before editing or before finalizing the patch:
  - use `just --list` to discover repo recipes when command routing is unclear
  - use `just sg-cs '<pattern>' Mods/QudJP/Assemblies/src` to compare repo-owned call shapes
  - use `just sg-cs '<pattern>'` with the default decompiled-source target when tracing upstream game producers
- Optional examples: try patterns such as `DynamicTextObservability.RecordTransform($$$ARGS)`, `Popup.Show($$$ARGS)`, or the method/class name you are changing.
- If structural search is intentionally skipped for C# route work, state the reason in the work note or PR summary.
- Constraints:
  - one patch class per file in `src/Patches/`
  - do not instantiate real game types in tests; use dummy targets with matching signatures
  - runtime Harmony comes from the game; tests use HarmonyLib NuGet `2.4.2`
  - producer or queue-gated translation patches must follow the route-contract test checklist in `docs/RULES.md`
