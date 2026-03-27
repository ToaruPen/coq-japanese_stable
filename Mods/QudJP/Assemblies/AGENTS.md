# Assemblies

C# mod DLL and automated tests for Harmony patch behavior.

## Area Map

- `QudJP.csproj` — mod DLL (`net48`)
- `QudJP.Tests/` — test project (`net10.0`)
- `src/Patches/` — Harmony patches (one class per file)
- `src/` — translators, renderers, observability helpers, shared utilities

## Build and Test

```bash
dotnet build Mods/QudJP/Assemblies/QudJP.csproj
dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj
dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter TestCategory=L1
dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter TestCategory=L2
dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter TestCategory=L2G
```

## Source of truth

- Patch behavior is defined by tests in `QudJP.Tests/`.
- Layer boundaries live in `docs/test-architecture.md`.
- Runtime-only conclusions should be backed by fresh game logs.

If a design note conflicts with tests or runtime evidence, follow tests first.

## Test Layers

| Layer | Scope | Dependencies |
|-------|-------|-------------|
| L1 | Pure logic | No HarmonyLib, no UnityEngine |
| L2 | Harmony integration | HarmonyLib NuGet 2.4.2, no Unity runtime |
| L2G | DLL-assisted resolution | Assembly-CSharp.dll for target/signature checks |
| L3 | Manual in-game verification | Rosetta on Apple Silicon |

## Translation Workflow Gate

Before adding any translation:

1. Check the relevant L1/L2/L2G coverage in `QudJP.Tests/`
2. Confirm the route is truly stable with current runtime evidence
3. Investigate the upstream producer first if route ownership is unclear

Prefer minimal, test-backed changes. Many sink and near-sink routes are intentionally observation-only; do not reintroduce sink-side dictionary translation unless tests explicitly require it.

## Game Source Reference

Decompiled game source: `~/Dev/coq-decompiled/` (outside repo, never committed).
Regenerate: `scripts/decompile_game_dll.sh`

Use to trace upstream producers, verify method signatures, investigate unclaimed routes.

## Constraints

- Do not commit `Assembly-CSharp.dll` or other game DLLs.
- Do not instantiate real game types in tests — use DummyTargets with matching signatures.
- One patch class per file in `src/Patches/`.
- Runtime Harmony comes from the game; tests use HarmonyLib NuGet `2.4.2`.

## Failure Model

- Initialization failures fail fast.
- `TargetMethod()` resolution failures log and skip patch application.
- Runtime Prefix/Postfix failures log via `Trace.TraceError` — never crash the game loop.
