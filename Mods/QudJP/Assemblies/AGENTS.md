# Assemblies AGENTS.md

## WHY

- This area contains the C# mod DLL loaded by the game and the automated tests that validate patch behavior.
- Most runtime localization logic, route/template handling, and Harmony patching lives here.

## WHAT

### Area Map

- `QudJP.csproj`
  - mod DLL project targeting `net48`
- `QudJP.Tests/`
  - test project targeting `net10.0`
- `src/`
  - production code, including `Patches/`, translator logic, and observability helpers

### Facts That Matter Here

- Runtime Harmony comes from the game environment; tests use HarmonyLib NuGet `2.4.2`.
- Game DLL references use local contributor paths and are not committed.
- `Assembly-CSharp.dll` may be referenced for target resolution, signature checks, and safe static behavior checks, but direct game-type instantiation in tests is not part of this repo's test pattern.
- Dynamic/procedural text work in this area uses `docs/logic-required-policy.md` as the default policy.
  - If the task starts from `Player.log`, `missing key`, `no pattern`, display-name composition, popup templates, or inventory state text, read that policy before widening a route/template family.

### Test Layers

- L1
  - pure logic, no HarmonyLib, no UnityEngine
- L2
  - Harmony integration without Unity runtime
- L2G
  - game-DLL-assisted target resolution / hook inventory checks
- L3
  - manual in-game verification only; see `docs/inventory-verification.md` when relevant

## HOW

### Common Commands

- Build: `dotnet build Mods/QudJP/Assemblies/QudJP.csproj`
- All C# tests: `dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj`
- L1 only: `dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter TestCategory=L1`
- L2 only: `dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter TestCategory=L2`
- L2G only: `dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter TestCategory=L2G`

### Patch And Test Workflow

- Prefer the smallest route-aware change over sink-only cleanup.
- For logic-required families, identify the upstream generator and slot structure before adding templates or broad regexes.
- Add tests at the same composition boundary as the change.
  - L1 for parser/template logic
  - L2 or L2G for the actual patch route and untranslated pass-through behavior

### Runtime Failure Model

- Initialization failures are expected to fail fast.
- `TargetMethod()` resolution failures log an error and skip patch application.
- Runtime Prefix/Postfix failures log via `Trace.TraceError` and avoid crashing the game loop.

### Area-Specific Constraints

- Do not commit `Assembly-CSharp.dll` or other game DLLs.
- Do not instantiate real game types in tests when a DummyTarget with matching signature is sufficient.
- One patch class per file remains the local convention in `src/Patches/`.
