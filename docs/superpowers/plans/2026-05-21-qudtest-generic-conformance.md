# QudTest Generic Conformance Extension

## Goal

Extend QudTest from final-text smoke fixtures into a generic conformance harness that can be used across localization areas, not just HSE. The first generic lane will verify that patch-owned runtime routes resolve against the current game DLL through the same `TargetMethod` / `TargetMethods` entrypoints Harmony uses.

This does not replace live runtime evidence. It fills the gap between static inventories and final-text fixture output:

- Static analysis says a route or patch exists.
- QudTest binding fixtures prove the patch target still resolves against the current game DLL.
- QudTest final-text fixtures prove selected route logic still produces the expected visible text.
- In-game `qudtest:*` runs still provide the strongest proof that the deployed mod, dictionaries, and runtime environment agree.

## Non-Goals

- Do not make this HSE-specific.
- Do not attempt to fixture every patch in this change.
- Do not claim binding checks prove actual Harmony patch application in a running game.
- Do not remove or weaken existing `TargetMethodResolutionTests`.
- Do not commit changes; this repository requires explicit user approval before commits.

## Design

### Fixture Schema

Keep schema version `1` and add optional fields to `QudTestCase`:

- `patch`: patch class name for binding checks.
- `expectedTargets`: newline-normalized list of expected resolved target signatures.

Existing final-text fixtures continue to use `input` and `expected`.

Example:

```json
{
  "id": "binding.campfire-describe-meal",
  "route": "patch-binding",
  "patch": "QudJP.Patches.CampfireDescribeMealTranslationPatch",
  "expectedTargets": [
    "XRL.World.Parts.Campfire|DescribeMeal|System.String|System.Collections.Generic.IReadOnlyList`1[[XRL.World.GameObject]]"
  ]
}
```

### Route

Add QudTest route `patch-binding`.

Behavior:

1. Resolve `case.patch` to a patch type in the QudJP assembly.
2. Invoke static private/public `TargetMethod()` when present.
3. Invoke static private/public `TargetMethods()` when present.
4. Convert each `MethodBase` to the existing normalized full signature format from `TargetMethodResolutionTests`:
   `DeclaringType|MethodName|ReturnType|ParamType...`.
5. Sort signatures ordinally for stable fixture comparison.
6. Join signatures with `\n` as the actual output.

Diagnostics:

- Missing patch type: fail with a clear diagnostic.
- Missing both target entrypoints: fail.
- Null or empty target set: fail.
- Invocation exceptions: fail with the inner exception type/message.

### Representative Fixtures

Add `Mods/QudJP/QudTest/fixtures/bindings-smoke.json` with representative coverage from multiple areas:

- Cooking/HSE-adjacent owner route: `CampfireDescribeMealTranslationPatch`
- Cooking final display route: `CookingRecipeDisplayNameTranslationPatch`
- Journal route: `JournalEntryDisplayTextPatch`
- Journal map note route: `JournalMapNoteDisplayTextPatch`
- Runtime multi-target route: `CookingRuntimeTranslationPatch`
- UI route: one small stable screen or line patch from existing `TargetMethodResolutionTests`

This is intentionally representative, not exhaustive. Exhaustive patch coverage can be generated later from existing `TargetMethodResolutionTests` or static inventory files.

### Expected Normalization

`expectedTargets` is structured fixture input, not an artifact-only field. Both C# runner output and Python inspection must compare the same canonical expected string:

- For normal final-text cases, canonical expected is `case.expected`.
- For `patch-binding` cases with `expectedTargets`, canonical expected is `string.Join("\n", expectedTargets)`.
- The artifact `case.expected` field must contain that canonical string so mismatches show expected vs actual signatures directly.
- `scripts/qudtest_inspect.py` must apply the same normalization when it reads fixtures; otherwise binding fixtures without `expected` would be invisible to the inspector.

### CLI and Artifact Flow

No new CLI binary is needed. The existing headless runner already accepts arbitrary commands:

```bash
just qudtest-headless qudtest:bindings .artifacts/qudtest-bindings
```

The inspector needs a matching fixture-side normalization path for `expectedTargets`.

## TDD Plan

1. Add failing C# tests in `Mods/QudJP/Assemblies/QudJP.Tests/L1/QudTestRuntimeHarnessTests.cs`:
   - `patch-binding` resolves a single `TargetMethod` patch.
   - `patch-binding` resolves a multi-target `TargetMethods` patch.
   - stale or wrong `expectedTargets` fails and preserves both expected and actual signatures in the case result.
   - missing patch type fails with a useful diagnostic.
2. Add failing Python/headless tests:
   - `scripts/tests/test_qudtest_headless.py` runs `qudtest:bindings`.
   - `scripts/tests/test_qudtest_inspect.py` proves fixture-side `expectedTargets` normalization catches mismatched artifact output.
3. Implement model, expected normalization, inspector normalization, and route support.
4. Add representative binding fixture.
5. Update documentation for the three QudTest lanes:
   - `runtime` / final-text route fixtures
   - `wish` / wish queue route fixtures
   - `bindings` / patch target resolution fixtures

## Verification

Focused verification:

```bash
just build
just test-l1
just python-test
just qudtest-headless qudtest:bindings .artifacts/qudtest-bindings
```

For debugging only, the focused C# equivalent is
`dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter "FullyQualifiedName~QudTestRuntimeHarnessTests"`.

Regression verification:

```bash
just build
just test-l1
just test-l2g
just python-check
just python-test
just qudtest-headless qudtest:runtime .artifacts/qudtest-runtime
just qudtest-headless qudtest:wish .artifacts/qudtest-wish
git diff --check
```

## Post-Implementation Use Against Current Work

After implementation, run QudTest against current fixtures and current patch resolution tests:

- `qudtest:bindings` checks whether the representative patch target bindings still resolve.
- `qudtest:runtime` checks current final-text route fixtures.
- `qudtest:wish` checks the wish queue route fixture.
- `TargetMethodResolutionTests` remains the broad existing static/runtime-DLL resolution suite.

Report:

- any failed QudTest case ids,
- expected vs actual signature/text for failures,
- whether failures indicate bad patch targets, wrong fixture expectations, missing dictionaries, or route logic problems,
- and explicitly state uncovered limitations.
