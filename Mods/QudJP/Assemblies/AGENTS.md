# Assemblies — C# Harmony Patch DLL

This directory contains the mod DLL (`QudJP.csproj`, net48) and its test project
(`QudJP.Tests/`, net10.0).

## Project Targets

| Project | Target | Purpose |
|---------|--------|---------|
| `QudJP.csproj` | net48 | Mod DLL loaded by Unity Mono at runtime |
| `QudJP.Tests.csproj` | net10.0 | Test runner; never shipped to players |

## Compiler Settings (QudJP.csproj)

```xml
<Nullable>enable</Nullable>
<TreatWarningsAsErrors>true</TreatWarningsAsErrors>
<EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
```

Zero warnings policy: every Roslyn warning is a build error.

## Assembly References

All game DLL references must use `<Private>false</Private>`:

```xml
<Reference Include="Assembly-CSharp">
  <HintPath>path/to/Assembly-CSharp.dll</HintPath>
  <Private>false</Private>
</Reference>
```

This prevents game DLLs from being copied to the output directory.
Never commit Assembly-CSharp.dll or any other game DLL to the repo.

## Harmony Patching Patterns

### Prefix — intercept before original runs

```csharp
[HarmonyPatch(typeof(TargetClass), "TargetMethod")]
public static class MyPatch
{
    public static bool Prefix(ref string __result, string arg)
    {
        // Return false to skip the original method entirely.
        __result = TranslateJapanese(arg);
        return false;
    }
}
```

### Postfix — modify result after original runs

```csharp
[HarmonyPatch(typeof(TargetClass), "TargetMethod")]
public static class MyPatch
{
    public static void Postfix(ref string __result)
    {
        // __result holds the original return value; mutate it here.
        __result = ApplyJapaneseFormatting(__result);
    }
}
```

### Key rules
- `__result` is the return value (ref for mutation)
- `__instance` is `this` for instance methods
- `___fieldName` (triple underscore) accesses private fields
- Prefix returning `false` skips the original AND all lower-priority prefixes

## Fail-Fast Error Handling

Fail-fast follows a 3-tier pattern.

## Investigation Priority

For rendering, localization, and UI regressions in the mod DLL, prioritize root-cause investigation and durable fixes over stopgap workarounds. Temporary fallbacks are acceptable to collect evidence or keep diagnosis moving, but they are not the target end state and should be removed once the failing path is understood.

### 1) Init time
`QudJPMod.ApplyHarmonyPatches` / `Translator.LoadTranslations` must throw immediately.

```csharp
if (harmony is null)
    throw new InvalidOperationException("Harmony is required for patch registration.");
```

### 2) Target resolution (`TargetMethod()`)
Emit `Trace.TraceError` with method name, then return `null`.

```csharp
var method = AccessTools.Method(typeof(Grammar), "Pluralize");
if (method is null)
    Trace.TraceError("[QudJP] TargetMethod missing: Grammar.Pluralize");
return method;
```

### 3) Runtime patch bodies (Prefix/Postfix)
Log with `Trace.TraceError`; do not throw into the game loop.

```csharp
try { __result = Translate(__result); }
catch (Exception ex) { Trace.TraceError("[QudJP] Runtime patch failed: {0}", ex); }
```

Never silently swallow exceptions. Never use bare `catch (Exception) { }`. Never use `if (x is null) return;` to hide unexpected nulls.

## File Naming Conventions

```
src/
  Patches/
    GrammarPatch.cs        # One patch class per file
    ConversationPatch.cs
  Translators/
    Translator.cs          # Core translation logic
    ColorCodePreserver.cs
```

One patch class per file. File name matches class name.

## Test Architecture

### L1 — Pure Logic (`[Category("L1")]`)

No HarmonyLib. No UnityEngine. Tests pure string/grammar logic.

```csharp
[TestFixture, Category("L1")]
public class TranslatorTests
{
    [Test]
    public void Translate_ReturnsJapanese_WhenKeyExists()
    {
        var result = Translator.Translate("Hello");
        Assert.That(result, Is.EqualTo("こんにちは"));
    }
}
```

### L2 — Harmony Integration (`[Category("L2")]`)

HarmonyLib NuGet 2.4.2 allowed. No UnityEngine. Tests patch application
against DummyTarget classes and, where safe, against real `Assembly-CSharp.dll`
method resolution/static behavior without Unity runtime.

```csharp
[TestFixture, Category("L2")]
public class GrammarPatchTests
{
    [Test]
    public void Patch_ModifiesOutput_WhenApplied()
    {
        var harmony = new Harmony("test.qudjp");
        harmony.PatchAll(typeof(GrammarPatch).Assembly);
        var result = new DummyGrammar().Pluralize("cat");
        Assert.That(result, Is.EqualTo("猫"));
    }
}
```

## DummyTarget Pattern (Critical)

NEVER instantiate types from Assembly-CSharp.dll in tests.
Assembly-CSharp.dll may be referenced in tests for target resolution, signature
checks, and Unity-runtime-free static behavior.
Create test doubles with matching method signatures:

```csharp
// Good: test double with matching signature
internal class DummyGrammar
{
    public string Pluralize(string noun) => noun + "s";
    public string MakePossessive(string noun) => noun + "'s";
}

// Bad: real game type (causes TypeInitializationException in test runner)
// var grammar = new XRL.Language.Grammar();
```

The DummyTarget must have the exact same method signature as the real class
so Harmony can patch it in L2 tests when direct real-type execution is not the
best fit.

## HarmonyLib Versions

| Context | Version | Source |
|---------|---------|--------|
| Runtime (mod) | 0Harmony 2.2.2.0 | Prefer game-bundled; fall back to `Lib.Harmony` 2.2.2 metadata-only reference when `0Harmony.dll` is absent locally |
| Tests | HarmonyLib 2.4.2 | NuGet, test project only |

The test project references HarmonyLib via NuGet. The mod project prefers the
game-bundled `0Harmony.dll` at runtime, but may use a conditional
`Lib.Harmony` reference (with `ExcludeAssets="runtime"`) so local builds still
compile when `0Harmony.dll` is not present.
