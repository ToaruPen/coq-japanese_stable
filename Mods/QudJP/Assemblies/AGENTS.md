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
just build
just check
just test-l1
just test-l2
just test-l2g
```

- Run `just test-l1`, `just test-l2`, and `just test-l2g` sequentially. Parallel
  local invocations can race on shared `ReferenceStubs`/NuGet restore outputs.

- Prefer producer-owned or stable mid-pipeline fixes. Many sink and near-sink routes are intentionally observation-only.
- Use `~/dev/coq-decompiled_stable/` to trace upstream producers, verify signatures, and investigate unclaimed routes.
- When a patch reflects into upstream game members, verify the real method
  signature in decompiled source before choosing `AccessTools.Method`
  parameter types. C# optional arguments still appear as real parameters to
  reflection, so a source call such as `RenderForUI()` may require resolving
  `RenderForUI(string, bool)`. Add an L2G signature/contract test when this
  reflection path controls runtime UI behavior.
- For C# patch, translator, observability, or target-method changes, use structural search before editing or before finalizing the patch:
  - use `just --list` to discover repo recipes when command routing is unclear
  - use `just sg-cs '<pattern>' Mods/QudJP/Assemblies/src` to compare repo-owned call shapes
  - use `just sg-cs '<pattern>'` with the default decompiled-source target when tracing upstream game producers
- Optional examples: try patterns such as `DynamicTextObservability.RecordTransform($$$ARGS)`, `Popup.Show($$$ARGS)`, or the method/class name you are changing.
- If structural search is intentionally skipped for C# route work, state the reason in the work note or PR summary.
- Runtime diagnostics must route through `RuntimeDiagnostics`: use
  `RuntimeDiagnostics.LogVerboseProbe(...)` for verbose runtime probes and
  `RuntimeDiagnostics.LogImportant(...)` only for build, error, or
  sink-required shipping signals. Direct probe log markers such as
  `[QudJP] NewProbe/v1:`, `[QudJP] SinkObserve/v1:`,
  `[QudJP] Translator: missing key`, and `no pattern for` are dev-only by
  default and are rejected by the release DLL verifier when they remain in a
  release artifact.
- Dev-only probes that touch Unity, TMP, or reflection should fail closed:
  catch probe-building exceptions inside the probe and return a no-op result so
  observability cannot change visible translation behavior. When a probe depends
  on `#if` guards, add L1 policy coverage for the caller guard and release
  no-op branch.
- For `UnityEngine.Object`-derived coroutine hosts, components, transforms, or
  UI objects, use `== null` / `!= null` when lifetime semantics matter. Pattern
  null checks such as `is null` bypass Unity fake-null behavior.
- Keep QJ004 as a narrow bypass guard, not a general C# logging or
  format-string analyzer. It should detect known verbose probe markers that
  are statically visible on direct logging calls. Do not expand it into
  arbitrary format reconstruction, exception message inspection, or broad
  Unity/System.Diagnostics logging API modeling unless the probe policy itself
  changes.
- When future probes need stronger guarantees, prefer tightening the
  centralized `RuntimeDiagnostics` API, marker convention, release verifier, or
  focused analyzer tests before adding broad static inference to QJ004.
- For tooltip, TMP, or RTF display fixes:
  - identify the upstream producer route before patching sinks; prefer
    `Look.GenerateTooltipInformation(GameObject)` or another pre-render owner
    when the UI route permits it
  - preserve root contract tokens and markup as indivisible text boundaries:
    Qud wrappers, `&`/`^` color codes, TMP/rich-text tags, `\x01`,
    `=variable.name=`, `{0}`, and `{12:format}`
  - prove both no-op fallback behavior and at least one route boundary with
    tests; use L2G target-resolution coverage when the upstream game signature
    is the contract
  - verify both normal game-DLL builds/tests and the no-game Release build path
    when a patch needs `#if HAS_GAME_DLL` fallback code
- Constraints:
  - one patch class per file in `src/Patches/`
  - do not instantiate real game types in L1/L2 tests; use dummy targets with matching signatures
  - L2G may use a minimal real game type invocation only for an upstream member
    contract that cannot be proven by target/signature resolution and
    DummyTarget behavior tests; follow `docs/test-architecture.md`
  - runtime Harmony comes from the game; tests use HarmonyLib NuGet `2.4.2`
  - producer or queue-gated translation patches must follow the route-contract test checklist in `docs/RULES.md`
