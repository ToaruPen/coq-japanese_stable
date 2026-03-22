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

## Test Layers

| Layer | Scope | Dependencies |
|-------|-------|-------------|
| L1 | Pure logic | No HarmonyLib, no UnityEngine |
| L2 | Harmony integration | HarmonyLib NuGet 2.4.2, no Unity runtime |
| L2G | DLL-assisted resolution | Assembly-CSharp.dll for target/signature checks |
| L3 | Manual in-game verification | Rosetta on Apple Silicon |

## Translation Workflow Gate

Before adding any translation, check `docs/contract-inventory.json`:

1. **Leaf/MarkupLeaf** → dictionary entry is appropriate
2. **Template/Builder/MessageFrame** → implement translation logic (patch or template route)
3. **Route not registered** → investigate upstream producer first. Do NOT add a dictionary entry.

This gate exists because exact-key dictionary entries for dynamically composed text create maintenance debt.

## Patch Architecture (Producer-First)

See `docs/producer-first-design.md` for the full architecture.

- **ContractRegistry**: design-time source of truth for route → contract type mappings.
- **ClaimRegistry**: runtime `ConditionalWeakTable<string, ClaimInfo>` tracking translated strings.
- **Category 1** (field-write patches): use `ClaimRegistry.Claim()` after rendering.
- **Category 2** (`__result` rewrite patches): Scope Exempt — no claim needed.
- **Category 3** (Leaf entries): sink-executed contract via `ContractRegistry` lookup.

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
