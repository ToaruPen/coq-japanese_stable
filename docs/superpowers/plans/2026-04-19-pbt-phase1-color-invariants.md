# PBT Phase 1 Color Invariants Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add the first safe property-based tests to the C# L1 suite so issue-376 color/restore invariants are enforced by generated inputs without replacing existing regression tests.

**Architecture:** Keep the first slice inside pure C# L1 helper logic. Introduce FsCheck + FsCheck.NUnit only in `QudJP.Tests`, add a small generator focused on supported color markup shapes, and use it to prove preservation properties for `ColorCodePreserver` before expanding to translator-level helpers.

**Tech Stack:** .NET 10, NUnit 4, FsCheck, FsCheck.NUnit, existing `QudJP.Tests` project

---

## File Map

- Modify: `Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj`
  - Add the minimal NuGet packages needed for C# property tests.
- Create: `Mods/QudJP/Assemblies/QudJP.Tests/L1/Pbt/ColorCodePreserverPropertyTests.cs`
  - First PBT file, limited to color-preservation invariants already established by issue-376 regressions.
- Create: `Mods/QudJP/Assemblies/QudJP.Tests/L1/Pbt/ColorCodePreserverArbitraries.cs`
  - Narrow generators for supported wrapper/code shapes so failures stay readable and deterministic.
- Keep: `Mods/QudJP/Assemblies/QudJP.Tests/L1/ColorCodePreserverTests.cs`
  - Existing example-based regressions remain unchanged and continue to prove exact broken shapes.

### Task 1: Add the minimal FsCheck integration

**Files:**
- Modify: `Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj`
- Test: `Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj`

- [ ] **Step 1: Write the failing package integration step**

Add these package references under the existing test dependencies:

```xml
<PackageReference Include="FsCheck" Version="3.3.2" />
<PackageReference Include="FsCheck.NUnit" Version="3.3.2" />
```

- [ ] **Step 2: Run restore/build to verify the project accepts the new references**

Run: `dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter FullyQualifiedName~ColorCodePreserverTests`

Expected: existing `ColorCodePreserverTests` still pass; no package-resolution or adapter errors.

### Task 2: Add narrow generators for supported color shapes

**Files:**
- Create: `Mods/QudJP/Assemblies/QudJP.Tests/L1/Pbt/ColorCodePreserverArbitraries.cs`
- Test: `Mods/QudJP/Assemblies/QudJP.Tests/L1/Pbt/ColorCodePreserverPropertyTests.cs`

- [ ] **Step 1: Write the failing property test scaffold that references a custom arbitrary**

Create the property test file with this skeleton:

```csharp
using FsCheck;
using FsCheck.NUnit;

namespace QudJP.Tests.L1.Pbt;

[TestFixture]
[Category("L1")]
public sealed class ColorCodePreserverPropertyTests
{
    [Property(Arbitrary = new[] { typeof(ColorCodePreserverArbitraries) }, MaxTest = 200)]
    public Property StripThenRestore_PreservesSupportedMarkup(ColorizedCase sample)
    {
        var (stripped, spans) = ColorCodePreserver.Strip(sample.Source);
        var restored = ColorCodePreserver.Restore(sample.TranslatedVisibleText, spans);

        return (stripped == sample.VisibleText && restored == sample.ExpectedRestored)
            .ToProperty();
    }
}
```

Expected: compile fails until `ColorizedCase` and `ColorCodePreserverArbitraries` exist.

- [ ] **Step 2: Implement the narrow generator types**

Create `ColorCodePreserverArbitraries.cs` with small, readable generators:

```csharp
using FsCheck;

namespace QudJP.Tests.L1.Pbt;

public sealed record ColorizedCase(string Source, string VisibleText, string TranslatedVisibleText, string ExpectedRestored);

public static class ColorCodePreserverArbitraries
{
    public static Arbitrary<ColorizedCase> ColorizedCases()
    {
        var visible = Arb.Generate<NonEmptyString>()
            .Select(static text => text.Get.Replace("{", string.Empty, StringComparison.Ordinal).Replace("}", string.Empty, StringComparison.Ordinal));

        var translated = Arb.Generate<NonEmptyString>()
            .Select(static text => "訳" + text.Get.Replace("{", string.Empty, StringComparison.Ordinal).Replace("}", string.Empty, StringComparison.Ordinal));

        var wrappers = Gen.Elements(
            (Open: "{{W|", Close: "}}"),
            (Open: "{{r|", Close: "}}"),
            (Open: "&G", Close: "&y"),
            (Open: "^r", Close: "^k"));

        return (from rawVisible in visible
                from rawTranslated in translated
                where !string.IsNullOrWhiteSpace(rawVisible)
                from wrapper in wrappers
                select new ColorizedCase(
                    wrapper.Open + rawVisible + wrapper.Close,
                    rawVisible,
                    rawTranslated,
                    wrapper.Open + rawTranslated + wrapper.Close))
            .ToArbitrary();
    }
}
```

- [ ] **Step 3: Run the new property test and make sure it passes deterministically**

Run: `dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter FullyQualifiedName~ColorCodePreserverPropertyTests`

Expected: PASS with a stable run count; no shrinking into unsupported brace/code syntax.

### Task 3: Add a second invariant that protects issue-376’s restore seam

**Files:**
- Modify: `Mods/QudJP/Assemblies/QudJP.Tests/L1/Pbt/ColorCodePreserverPropertyTests.cs`

- [ ] **Step 1: Write the second failing property**

Add a property that proves non-target text is not damaged by the strip/restore cycle:

```csharp
[Property(Arbitrary = new[] { typeof(ColorCodePreserverArbitraries) }, MaxTest = 200)]
public Property StripThenRestore_DoesNotChangeVisibleText(ColorizedCase sample)
{
    var (stripped, spans) = ColorCodePreserver.Strip(sample.Source);
    var restored = ColorCodePreserver.Restore(stripped, spans);

    return (restored == sample.Source).ToProperty();
}
```

- [ ] **Step 2: Run just the new property and verify it passes**

Run: `dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter FullyQualifiedName~StripThenRestore_DoesNotChangeVisibleText`

Expected: PASS

### Task 4: Re-run the L1 baseline and preserve existing exact regressions

**Files:**
- Test: `Mods/QudJP/Assemblies/QudJP.Tests/L1/ColorCodePreserverTests.cs`
- Test: `Mods/QudJP/Assemblies/QudJP.Tests/L1/Pbt/ColorCodePreserverPropertyTests.cs`

- [ ] **Step 1: Run the helper-focused subset**

Run: `dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter FullyQualifiedName~ColorCodePreserver`

Expected: existing example-based tests and new property tests both pass.

- [ ] **Step 2: Run the full L1 suite**

Run: `dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj --filter TestCategory=L1`

Expected: PASS

### Task 5: Document the next PBT expansion seam

**Files:**
- Modify: `docs/superpowers/plans/2026-04-19-pbt-phase1-color-invariants.md`

- [ ] **Step 1: Record the next candidate after ColorCodePreserver**

Append a short execution note after successful verification:

```md
## Follow-up after Phase 1

- Next candidate: `MessageLogProducerTranslationHelpers`
- First invariant: control-header stripping plus direct-marker normalization must preserve nested wrappers
- Keep `MessagePatternTranslator` for Phase 1b or Phase 2, because its template grammar increases generator complexity
```

- [ ] **Step 2: Keep the scope boundary explicit**

Do not add Python/Hypothesis work in this first execution slice.

## Self-review

- Spec coverage: The plan covers issue-378’s first staged C# slice and uses issue-376’s color/restore invariants as the source of properties.
- Placeholder scan: No `TODO`/`TBD`; every task names exact files, code, and commands.
- Type consistency: `ColorizedCase` and `ColorCodePreserverArbitraries` are defined before their use in the property tests.

## Execution note after first slice

- Phase 1 was implemented with deterministic FsCheck + NUnit properties for `ColorCodePreserver` using only Qud wrapper shapes (`{{W|...}}`, `{{r|...}}`).
- The first generated failures showed that `&` / `^` color codes do not share the same restore invariant as Qud wrappers when mixed into one property family.
- Phase 1b was therefore implemented as separate property groups for foreground/background color codes instead of widening the existing wrapper generator.
- The next non-trivial candidate, `MessageLogProducerTranslationHelpers`, was added in the same PR as a deterministic PBT slice for the `control header -> direct marker -> nested wrapper` invariant.
- `MessagePatternTranslator` was then added as the next deterministic slice for hit-with-roll and weapon-miss wrapper-preservation invariants using the repository pattern dictionary directly.
- `MessageFrameTranslator` was then added as a small deterministic helper slice for direct-marker idempotence and strip round-trips, keeping the scope outside combat grammar expansion.
- `EnclosingFragmentTranslator` was added next as a constrained route-owner slice for `You extricate X from Y.` so subject/container color wrappers stay preserved while direct-marked inputs still pass through unchanged.
- `ClonelingVehicleFragmentTranslator` was then added as the next world-part fragment slice, covering both popup and queued routes while preserving liquid wrappers and leaving direct-marked text untouched.
- `LiquidVolumeFragmentTranslator` followed with a deliberately narrow generator surface that covers status wrappers, ownership questions, normalized targets, pour-into targets, and known passthrough inputs without over-generalizing the fragment grammar.
- The next follow-up after this PR should likely move back to a smaller helper seam again before widening route grammar further.
