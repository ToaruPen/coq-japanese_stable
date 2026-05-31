# Inventory Menu Performance Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Reduce inventory context-menu open/close sluggishness without depending on dev probe removal, including bottom context menu translation, inventory row translation, reflection access, and TMP font refresh repeat work.

**Architecture:** Keep producer data stable and move menu-row localization to display-only rendering. Cache pure translation/reflection decisions at type and source-text boundaries, while preserving vanilla data contracts such as `QudMenuItem.text`, `QudMenuItem.simpleText`, and tutorial `ControlId` values. Gate expensive TMP refreshes with existing healthy-refresh keys so `InventoryLine.setData` and active-row refresh paths do not both force mesh/font work for an unchanged row.

**Tech Stack:** C# net48/net10-compatible QudJP Harmony patches, NUnit L1/L2/L2G tests, decompiled Caves of Qud 1.0.4 reference source under `~/dev/coq-decompiled_stable/`, `just` validation recipes.

---

## Current Findings

- `EquipmentAPI.ShowInventoryActionMenu` opens action choices with `Popup.PickOption(... AllowEscape: true ...)`; cancel returns `null`, then `InventoryAndEquipmentStatusScreen.HandleSelectItem` calls `UpdateViewFromData()` when `bDone` is false. That rebuilds inventory rows after both menu open and menu close.
- `Popup.PickOption` builds `QudMenuItem` values. Inventory action menu option translation is intentionally skipped in `PopupPickOptionTranslationPatch`, `PopupGetPopupOptionTranslationPatch`, and `PopupMessageTranslationPatch` to preserve tutorial/control IDs.
- `QudMenuBottomContext.Update()` calls `RefreshButtons()` every frame. QudJP currently prefixes `RefreshButtons()` and mutates `items[index].text` through `QudMenuBottomContextTranslationPatch.NormalizeItemTexts(...)`, so the bottom context path can redo translation work at frame rate.
- `SelectableTextMenuItemTranslationPatch` already performs display-only translation by calling `item.SetText(...)` after vanilla `SelectChanged(bool)`, without mutating `QudMenuItem` data. This is the safer boundary for menu labels.
- `InventoryLineTranslationPatch.Postfix` runs after visible inventory rows are rebound. It repeats reflection lookup, translation route work, `OwnerTextSetter`, and `InventoryLineFontFixer.TryRefreshTextSkinWithFallbackFont(...)`.
- `InventoryLineRenderProbePatch.Postfix` also calls `InventoryLineFontFixer.TryApplyPrimaryFontToItemRow(...)` on the same `InventoryLine.setData` target under `HAS_TMP`, which can duplicate font refresh work after `InventoryLineTranslationPatch` has already refreshed the final translated text.
- `Mods/QudJP/Assemblies/QudJP.Tests/L2/InventoryLineTranslationPatchTests.cs` declares `Issue201StatusScreensBatch2Tests`, so focused filters must use `FullyQualifiedName~Issue201StatusScreensBatch2Tests`.

## File Structure

- Modify `Mods/QudJP/Assemblies/src/Patches/QudMenuBottomContextTranslationPatch.cs`: keep probe logging only; stop mutating `QudMenuItem` data during `RefreshButtons`.
- Modify `Mods/QudJP/Assemblies/src/Patches/SelectableTextMenuItemTranslationPatch.cs`: own display-only menu text normalization, translation, and bounded display cache.
- Modify `Mods/QudJP/Assemblies/src/Patches/InventoryLineTranslationPatch.cs`: add bounded caches for inventory visible text/display-name translation and use cached reflection accessors for hot fields.
- Modify `Mods/QudJP/Assemblies/src/Patches/UITextSkinReflectionAccessor.cs`: cache `SetText`/field/property access strategies per UI text skin type.
- Modify `Mods/QudJP/Assemblies/src/UI/ReflectionUtils.cs`: cache property/field getters used by inventory font and observability paths.
- Modify `Mods/QudJP/Assemblies/src/UI/InventoryLineFontFixer.cs`: add a setData refresh wrapper that checks/records healthy refresh keys and avoids repeated mesh/font work for the same final row text.
- Modify `Mods/QudJP/Assemblies/src/Patches/InventoryLineRenderProbePatch.cs`: call the new gated setData refresh wrapper, or skip if the translation patch has already recorded a healthy refresh for the current key.
- Test updates:
  - `Mods/QudJP/Assemblies/QudJP.Tests/L2/QudMenuBottomContextTranslationPatchTests.cs`
  - `Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupPickOptionTranslationPatchTests.cs`
  - `Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupTranslationPatchTests.cs`
  - `Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupRouteHandoffTranslationTests.cs`
  - `Mods/QudJP/Assemblies/QudJP.Tests/L2/InventoryLineTranslationPatchTests.cs`
  - `Mods/QudJP/Assemblies/QudJP.Tests/L1/ColorRouteCatalogTests.cs`
  - `Mods/QudJP/Assemblies/QudJP.Tests/L1/InventoryLineRenderProbePatchTests.cs`
  - `Mods/QudJP/Assemblies/QudJP.Tests/L1/InventoryLineFontFixerPolicyTests.cs`
  - `Mods/QudJP/Assemblies/QudJP.Tests/L1/ReflectionUtilsTests.cs` if no suitable existing reflection-cache policy test exists.

## Task 1: Move Bottom Context Translation To Display-Only

**Files:**
- Modify: `Mods/QudJP/Assemblies/src/Patches/QudMenuBottomContextTranslationPatch.cs`
- Modify: `Mods/QudJP/Assemblies/src/Patches/SelectableTextMenuItemTranslationPatch.cs`
- Test: `Mods/QudJP/Assemblies/QudJP.Tests/L2/QudMenuBottomContextTranslationPatchTests.cs`
- Test: `Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupPickOptionTranslationPatchTests.cs`
- Test: `Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupTranslationPatchTests.cs`
- Test: `Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupRouteHandoffTranslationTests.cs`
- Test: `Mods/QudJP/Assemblies/QudJP.Tests/L1/ColorRouteCatalogTests.cs`

- [ ] **Step 1: Write failing tests for non-mutating bottom context data**

Replace the mutation expectations in `QudMenuBottomContextTranslationPatchTests` with tests that prove `RefreshButtons` no longer changes `context.items[*].text`:

```csharp
[Test]
public void Prefix_DoesNotMutateMenuItemText()
{
    WriteDictionary(("Inspect", "調べる"));

    var context = new DummyQudMenuBottomContext("Inspect");
    RunRefreshButtonsWithPatch(context);

    Assert.Multiple(() =>
    {
        Assert.That(((DummyMenuItem)context.items[0]!).text, Is.EqualTo("Inspect"));
        Assert.That(
            DynamicTextObservability.GetRouteFamilyHitCountForTests(
                nameof(QudMenuBottomContextTranslationPatch),
                "Popup.ProducerMenuItem.Exact"),
            Is.Zero);
    });
}

[Test]
public void Prefix_DoesNotFlattenNestedHotkeyData()
{
    var source = "{{y|{{W|[Esc]}} Back}}";
    var context = new DummyQudMenuBottomContext(source);

    RunRefreshButtonsWithPatch(context);

    Assert.That(((DummyMenuItem)context.items[0]!).text, Is.EqualTo(source));
}
```

Add display-path coverage in `PopupPickOptionTranslationPatchTests`:

```csharp
[Test]
public void TranslateMenuItemTextForDisplay_FlattensAndTranslatesNestedBottomContextHotkeyLabel()
{
    WriteCommonMenuActionDictionary(("back", "戻る"));

    var translated = SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay(
        "{{y|{{W|[Esc]}} Back}}");

    Assert.That(translated, Is.EqualTo("{{W|[Esc]}} {{y|戻る}}"));
}

[Test]
public void SelectableTextMenuItemPostfix_TranslatesDisplayOnlyAndPreservesControlIdData()
{
    WriteCommonMenuActionDictionary(("get", "取る"));
    var menuData = new DummySelectableMenuData(
        text: "{{W|[g]}} {{y|get}}",
        simpleText: "get",
        command: "CmdGet",
        hotkey: "g");
    var target = new DummySelectableTextMenuItemTarget(menuData);

    SelectableTextMenuItemTranslationPatch.Postfix(target, newState: true);

    Assert.Multiple(() =>
    {
        Assert.That(menuData.text, Is.EqualTo("{{W|[g]}} {{y|get}}"));
        Assert.That(menuData.simpleText, Is.EqualTo("get"));
        Assert.That(menuData.command, Is.EqualTo("CmdGet"));
        Assert.That(menuData.hotkey, Is.EqualTo("g"));
        Assert.That(target.ControlId, Is.EqualTo("QudTextMenuItem:get"));
        Assert.That(target.item.Text, Is.EqualTo("{{W|{{W|[g]}} {{y|取る}}}}"));
    });
}
```

Add these dummy types to the same test file:

```csharp
private sealed class DummySelectableTextMenuItemTarget
{
    public DummySelectableTextMenuItemTarget(DummySelectableMenuData data)
    {
        this.data = data;
        itemText = data.text;
        ControlId = "QudTextMenuItem:" + data.simpleText;
    }

    public DummySelectableMenuData data { get; }

    public string itemText { get; }

    public string ControlId { get; }

    public readonly DummySelectableItemSkin item = new();
}

private sealed class DummySelectableItemSkin
{
    public string Text { get; private set; } = string.Empty;

    public void SetText(string value)
    {
        Text = value;
    }
}

private sealed class DummySelectableMenuData
{
    public DummySelectableMenuData(string text, string simpleText, string command, string hotkey)
    {
        this.text = text;
        this.simpleText = simpleText;
        this.command = command;
        this.hotkey = hotkey;
    }

    public string text;
    public string simpleText;
    public string command;
    public string hotkey;
}
```

Update `PopupTranslationPatchTests.NormalizeItemTexts_*` and `PopupRouteHandoffTranslationTests` so they assert preserved bottom-context data and display-path translation through `SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay(...)`.

- [ ] **Step 2: Run the focused failing tests**

Run:

```bash
dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj \
  --filter "FullyQualifiedName~QudMenuBottomContextTranslationPatchTests|FullyQualifiedName~PopupPickOptionTranslationPatchTests|FullyQualifiedName~PopupTranslationPatchTests|FullyQualifiedName~PopupRouteHandoffTranslationTests|FullyQualifiedName~ColorRouteCatalogTests" \
  --nologo
```

Expected: failures still reflect old behavior: bottom context data is translated/mutated by `QudMenuBottomContextTranslationPatch.NormalizeItemTexts(...)`, and nested bottom context labels are not yet normalized by the display path.

- [ ] **Step 3: Stop bottom context data mutation**

Change `QudMenuBottomContextTranslationPatch.Prefix` so it only probes:

```csharp
public static void Prefix(object __instance)
{
    try
    {
        LogProbe(__instance, "prefix");
    }
    catch (Exception ex)
    {
        Trace.TraceError("QudJP: QudMenuBottomContextTranslationPatch.Prefix failed: {0}", ex);
    }
}
```

Keep `NormalizeItemTexts(object? contextInstance)` as an internal compatibility shim for tests and older call sites, but make it a no-op with an explanatory comment:

```csharp
internal static void NormalizeItemTexts(object? contextInstance)
{
    // QudMenuBottomContext.RefreshButtons runs every frame. Do not mutate
    // QudMenuItem data here; display translation belongs to
    // SelectableTextMenuItemTranslationPatch so tutorial/control IDs remain stable.
    _ = contextInstance;
}
```

Remove the `Regex NestedHotkeyLabelPattern` and translation loop from this file after moving the helper to `SelectableTextMenuItemTranslationPatch`.

- [ ] **Step 4: Move nested hotkey normalization into the display path**

Add the regex and helper to `SelectableTextMenuItemTranslationPatch`:

```csharp
private static readonly Regex NestedHotkeyLabelPattern =
    new Regex(
        @"^\{\{(?<labelColor>[A-Za-z]+)\|\{\{(?<hotkeyColor>[A-Za-z]+)\|(?<hotkey>\[[^\]]+\])\}\}\s*(?<label>.*?)\}\}$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

internal static string NormalizeNestedHotkeyLabelForDisplay(string source)
{
    var match = NestedHotkeyLabelPattern.Match(source);
    if (!match.Success)
    {
        return source;
    }

    var hotkey = match.Groups["hotkey"].Value;
    var label = match.Groups["label"].Value;
    if (label.Length == 0)
    {
        return source;
    }

    return "{{"
        + match.Groups["hotkeyColor"].Value
        + "|"
        + hotkey
        + "}} {{"
        + match.Groups["labelColor"].Value
        + "|"
        + label
        + "}}";
}
```

Update `TranslateMenuItemTextForDisplay` to normalize before translating:

```csharp
internal static string TranslateMenuItemTextForDisplay(string source, string? popupId)
{
    var normalized = NormalizeNestedHotkeyLabelForDisplay(source);
    return MessageFrameTranslator.StripAllDirectTranslationMarkers(
        PopupTranslationPatch.TranslatePopupMenuItemTextForProducerRoute(normalized, Context, popupId));
}
```

Add `using System.Text.RegularExpressions;` to the file.

- [ ] **Step 5: Update route catalog policy**

In `ColorRouteCatalogTests`, remove the expected `PopupTranslationPatch.TranslatePopupMenuItemTextForProducerRoute(...)` producer route from `QudMenuBottomContextTranslationPatch.cs` and keep the existing `SelectableTextMenuItemTranslationPatch.cs` route expectation.

- [ ] **Step 6: Re-run focused tests**

Run the same focused command from Step 2.

Expected: all selected tests pass.

## Task 2: Add Bounded Display Translation Cache

**Files:**
- Modify: `Mods/QudJP/Assemblies/src/Patches/SelectableTextMenuItemTranslationPatch.cs`
- Test: `Mods/QudJP/Assemblies/QudJP.Tests/L2/PopupPickOptionTranslationPatchTests.cs`

- [ ] **Step 1: Write failing cache tests**

Add setup/teardown calls in `PopupPickOptionTranslationPatchTests`:

```csharp
SelectableTextMenuItemTranslationPatch.ClearDisplayTranslationCacheForTests();
```

Add tests:

```csharp
[Test]
public void TranslateMenuItemTextForDisplay_CachesRepeatedSourceAndPopupId()
{
    WriteCommonMenuActionDictionary(("get", "取る"));

    var first = SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay(
        "{{W|[g]}} {{y|get}}",
        "InventoryActionMenu:test");
    var second = SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay(
        "{{W|[g]}} {{y|get}}",
        "InventoryActionMenu:test");

    Assert.Multiple(() =>
    {
        Assert.That(first, Is.EqualTo("{{W|[g]}} {{y|取る}}"));
        Assert.That(second, Is.EqualTo(first));
        Assert.That(SelectableTextMenuItemTranslationPatch.GetDisplayTranslationCacheCountForTests(), Is.EqualTo(1));
    });
}

[Test]
public void TranslateMenuItemTextForDisplay_CacheSeparatesPopupIds()
{
    WriteCommonMenuActionDictionary(("get", "取る"));

    _ = SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay("{{W|[g]}} {{y|get}}", "InventoryActionMenu:a");
    _ = SelectableTextMenuItemTranslationPatch.TranslateMenuItemTextForDisplay("{{W|[g]}} {{y|get}}", "InventoryActionMenu:b");

    Assert.That(SelectableTextMenuItemTranslationPatch.GetDisplayTranslationCacheCountForTests(), Is.EqualTo(2));
}
```

- [ ] **Step 2: Run tests to verify failure**

Run:

```bash
dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj \
  --filter "FullyQualifiedName~PopupPickOptionTranslationPatchTests" \
  --nologo
```

Expected: compile failure because `ClearDisplayTranslationCacheForTests` and `GetDisplayTranslationCacheCountForTests` do not exist.

- [ ] **Step 3: Implement bounded cache**

Add to `SelectableTextMenuItemTranslationPatch`:

```csharp
private const int MaxDisplayTranslationCacheEntries = 2048;
private static readonly ConcurrentDictionary<DisplayTranslationCacheKey, string> DisplayTranslationCache = new();
private static readonly ConcurrentQueue<DisplayTranslationCacheKey> DisplayTranslationCacheOrder = new();

internal static void ClearDisplayTranslationCacheForTests()
{
    DisplayTranslationCache.Clear();
    while (DisplayTranslationCacheOrder.TryDequeue(out _))
    {
    }
}

internal static int GetDisplayTranslationCacheCountForTests()
{
    return DisplayTranslationCache.Count;
}

private static string TranslateMenuItemTextForDisplayUncached(DisplayTranslationCacheKey key)
{
    var normalized = NormalizeNestedHotkeyLabelForDisplay(key.Source);
    return MessageFrameTranslator.StripAllDirectTranslationMarkers(
        PopupTranslationPatch.TranslatePopupMenuItemTextForProducerRoute(normalized, Context, key.PopupId));
}

private static void RememberDisplayTranslationKey(DisplayTranslationCacheKey key)
{
    DisplayTranslationCacheOrder.Enqueue(key);
    while (DisplayTranslationCache.Count > MaxDisplayTranslationCacheEntries
        && DisplayTranslationCacheOrder.TryDequeue(out var oldest))
    {
        DisplayTranslationCache.TryRemove(oldest, out _);
    }
}

private readonly struct DisplayTranslationCacheKey : IEquatable<DisplayTranslationCacheKey>
{
    internal DisplayTranslationCacheKey(string source, string? popupId)
    {
        Source = source;
        PopupId = popupId ?? string.Empty;
    }

    internal string Source { get; }

    internal string PopupId { get; }

    public bool Equals(DisplayTranslationCacheKey other)
    {
        return string.Equals(Source, other.Source, StringComparison.Ordinal)
            && string.Equals(PopupId, other.PopupId, StringComparison.Ordinal);
    }

    public override bool Equals(object? obj)
    {
        return obj is DisplayTranslationCacheKey other && Equals(other);
    }

    public override int GetHashCode()
    {
        return StringComparer.Ordinal.GetHashCode(Source) * 397
            ^ StringComparer.Ordinal.GetHashCode(PopupId);
    }
}
```

Update `TranslateMenuItemTextForDisplay`:

```csharp
internal static string TranslateMenuItemTextForDisplay(string source, string? popupId)
{
    var key = new DisplayTranslationCacheKey(source, popupId);
    if (DisplayTranslationCache.TryGetValue(key, out var cached))
    {
        return cached;
    }

    var translated = TranslateMenuItemTextForDisplayUncached(key);
    if (DisplayTranslationCache.TryAdd(key, translated))
    {
        RememberDisplayTranslationKey(key);
    }

    return translated;
}
```

Add `using System.Collections.Concurrent;`.

- [ ] **Step 4: Run tests**

Run the same command from Step 2.

Expected: `PopupPickOptionTranslationPatchTests` pass.

## Task 3: Cache Inventory Line Translation And Reflection Access

**Files:**
- Modify: `Mods/QudJP/Assemblies/src/Patches/InventoryLineTranslationPatch.cs`
- Modify: `Mods/QudJP/Assemblies/src/Patches/UITextSkinReflectionAccessor.cs`
- Modify: `Mods/QudJP/Assemblies/src/UI/ReflectionUtils.cs`
- Test: `Mods/QudJP/Assemblies/QudJP.Tests/L2/InventoryLineTranslationPatchTests.cs` (`Issue201StatusScreensBatch2Tests`)
- Test: `Mods/QudJP/Assemblies/QudJP.Tests/L1/ReflectionUtilsTests.cs`

- [ ] **Step 1: Write failing inventory cache tests**

Add to `Issue201StatusScreensBatch2Tests` setup/teardown in `InventoryLineTranslationPatchTests.cs`:

```csharp
InventoryLineTranslationPatch.ClearTranslationCachesForTests();
```

Add:

```csharp
[Test]
public void TranslateItemDisplayNameForQudTest_CachesRepeatedDisplayName()
{
    WriteDictionaryFile("ui-displayname-atomic.ja.json", ("water flask", "水袋"));

    var first = InventoryLineTranslationPatch.TranslateItemDisplayNameForQudTest("water flask");
    var second = InventoryLineTranslationPatch.TranslateItemDisplayNameForQudTest("water flask");

    Assert.Multiple(() =>
    {
        Assert.That(first, Is.EqualTo("水袋"));
        Assert.That(second, Is.EqualTo(first));
        Assert.That(InventoryLineTranslationPatch.GetTranslationCacheCountForTests(), Is.GreaterThanOrEqualTo(1));
    });
}
```

Add an L2 companion assertion near the inventory display-name cache tests so the
cache policy is tied to real localization output, not only to accessor counts:

```csharp
[Test]
public void TranslateItemDisplayNameForQudTest_CachesRepeatedDisplayName()
{
    WriteDictionaryFile("ui-displayname-atomic.ja.json", ("water flask", "水袋"));

    var initialCacheCount = InventoryLineTranslationPatch.GetTranslationCacheCountForTests();
    var first = InventoryLineTranslationPatch.TranslateItemDisplayNameForQudTest("water flask");
    var afterFirstCacheCount = InventoryLineTranslationPatch.GetTranslationCacheCountForTests();
    var second = InventoryLineTranslationPatch.TranslateItemDisplayNameForQudTest("water flask");

    Assert.Multiple(() =>
    {
        Assert.That(first, Is.EqualTo("水袋"));
        Assert.That(second, Is.EqualTo(first));
        Assert.That(afterFirstCacheCount, Is.EqualTo(initialCacheCount + 1));
        Assert.That(InventoryLineTranslationPatch.GetTranslationCacheCountForTests(), Is.EqualTo(afterFirstCacheCount));
    });
}
```

Modify the existing `ReflectionUtilsTests` file and add this cache test:

```csharp
namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
public sealed class ReflectionUtilsTests
{
    [Test]
    public void GetPropertyOrFieldValue_CachesResolvedMemberAccessor()
    {
        ReflectionUtils.ClearAccessorCacheForTests();
        var target = new ReflectionTarget { Name = "alpha" };

        Assert.That(ReflectionUtils.GetPropertyOrFieldValue(target, "Name"), Is.EqualTo("alpha"));
        Assert.That(ReflectionUtils.GetPropertyOrFieldValue(target, "Name"), Is.EqualTo("alpha"));

        Assert.That(ReflectionUtils.GetAccessorCacheCountForTests(), Is.EqualTo(1));
    }

    private sealed class ReflectionTarget
    {
        public string Name { get; set; } = string.Empty;
    }
}
```

Also add this helper to the same existing `ReflectionUtilsTests` file before adding source-policy tests that call `ExtractMethodBody`:

```csharp
private static string ExtractMethodBody(string source, string signature)
{
    var start = source.IndexOf(signature, StringComparison.Ordinal);
    Assert.That(start, Is.GreaterThanOrEqualTo(0), "method signature not found: " + signature);
    var braceStart = source.IndexOf('{', start);
    Assert.That(braceStart, Is.GreaterThanOrEqualTo(0), "method body not found: " + signature);

    var depth = 0;
    for (var index = braceStart; index < source.Length; index++)
    {
        if (source[index] == '{')
        {
            depth++;
        }
        else if (source[index] == '}')
        {
            depth--;
            if (depth == 0)
            {
                return source.Substring(braceStart, index - braceStart + 1);
            }
        }
    }

    Assert.Fail("method body did not terminate: " + signature);
    return string.Empty;
}
```

- [ ] **Step 2: Run tests to verify failure**

Run:

```bash
dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj \
  --filter "FullyQualifiedName~Issue201StatusScreensBatch2Tests|FullyQualifiedName~ReflectionUtilsTests" \
  --nologo
```

Expected: compile failure for missing cache helper methods.

- [ ] **Step 3: Add inventory translation cache**

In `InventoryLineTranslationPatch`, add a bounded cache similar to Task 2:

```csharp
private const int MaxTranslationCacheEntries = 4096;
private static readonly ConcurrentDictionary<InventoryTranslationCacheKey, string> TranslationCache = new();
private static readonly ConcurrentQueue<InventoryTranslationCacheKey> TranslationCacheOrder = new();

internal static void ClearTranslationCachesForTests()
{
    TranslationCache.Clear();
    while (TranslationCacheOrder.TryDequeue(out _))
    {
    }
}

internal static int GetTranslationCacheCountForTests()
{
    return TranslationCache.Count;
}
```

Use it from `TranslateVisibleText` and `TranslateItemDisplayName`. The cache key must include the source text and translation family/route category, not object identity:

```csharp
private static string GetOrAddTranslationCache(
    string family,
    string source,
    Func<string> translate)
{
    var key = new InventoryTranslationCacheKey(family, source);
    if (TranslationCache.TryGetValue(key, out var cached))
    {
        return cached;
    }

    var translated = translate();
    if (TranslationCache.TryAdd(key, translated))
    {
        TranslationCacheOrder.Enqueue(key);
        while (TranslationCache.Count > MaxTranslationCacheEntries
            && TranslationCacheOrder.TryDequeue(out var oldest))
        {
            TranslationCache.TryRemove(oldest, out _);
        }
    }

    return translated;
}
```

Wrap the expensive branch in `TranslateItemDisplayName`:

```csharp
private static string TranslateItemDisplayName(string source, string route)
{
    return GetOrAddTranslationCache("InventoryLine.ItemName", source, () =>
        TranslateItemDisplayNameUncached(source, route));
}
```

Move the current body of `TranslateItemDisplayName` into `TranslateItemDisplayNameUncached`.

- [ ] **Step 4: Cache reflection accessors**

In `InventoryLineTranslationPatch`, replace the private `GetMemberValue` implementation so the inventory hot path uses the shared cached accessor:

```csharp
private static object? GetMemberValue(object instance, string memberName)
{
    return ReflectionUtils.GetPropertyOrFieldValue(instance, memberName);
}
```

Keep `GetStaticMemberValue` local because it reads static members from `XRL.UI.Options`.

Add a source-policy test in `ReflectionUtilsTests`:

```csharp
[Test]
public void InventoryLineTranslationPatch_UsesSharedReflectionUtilsForHotMembers()
{
    var sourcePath = Path.Combine(
        TestProjectPaths.GetRepositoryRoot(),
        "Mods",
        "QudJP",
        "Assemblies",
        "src",
        "Patches",
        "InventoryLineTranslationPatch.cs");
    var source = File.ReadAllText(sourcePath);
    var method = ExtractMethodBody(source, "private static object? GetMemberValue");

    Assert.That(method, Does.Contain("ReflectionUtils.GetPropertyOrFieldValue(instance, memberName)"));
    Assert.That(method, Does.Not.Contain("AccessTools.Property(type, memberName)"));
    Assert.That(method, Does.Not.Contain("AccessTools.Field(type, memberName)"));
}
```

In `ReflectionUtils`, replace per-call member search with a per-type/member accessor cache:

```csharp
private static readonly ConcurrentDictionary<MemberAccessorKey, Func<object, object?>> Accessors = new();

internal static void ClearAccessorCacheForTests()
{
    Accessors.Clear();
}

internal static int GetAccessorCacheCountForTests()
{
    return Accessors.Count;
}
```

Build the accessor once with the same base-type walk as the current implementation. If no property/field exists, cache a lambda returning `null` so misses do not repeat reflection.

In `UITextSkinReflectionAccessor`, cache the `SetText` method/field/property strategy per UI text skin type. Keep existing fallback warning behavior, but log a fallback warning only when the strategy is created, not on every row update.

Add a source-policy test to `ReflectionUtilsTests`:

```csharp
[Test]
public void UITextSkinReflectionAccessor_CachesTextWriteStrategiesByType()
{
    var sourcePath = Path.Combine(
        TestProjectPaths.GetRepositoryRoot(),
        "Mods",
        "QudJP",
        "Assemblies",
        "src",
        "Patches",
        "UITextSkinReflectionAccessor.cs");
    var source = File.ReadAllText(sourcePath);

    Assert.That(source, Does.Contain("ConcurrentDictionary<Type,"));
    Assert.That(source, Does.Contain("GetOrAdd"));
    Assert.That(source, Does.Contain("SetText"));
}
```

- [ ] **Step 5: Run focused tests**

Run the command from Step 2.

Expected: all selected tests pass.

## Task 4: Gate Duplicate TMP Font Refresh On Inventory setData

**Files:**
- Modify: `Mods/QudJP/Assemblies/src/UI/InventoryLineFontFixer.cs`
- Modify: `Mods/QudJP/Assemblies/src/Patches/InventoryLineTranslationPatch.cs`
- Modify: `Mods/QudJP/Assemblies/src/Patches/InventoryLineRenderProbePatch.cs`
- Test: `Mods/QudJP/Assemblies/QudJP.Tests/L1/InventoryLineRenderProbePatchTests.cs`
- Test: `Mods/QudJP/Assemblies/QudJP.Tests/L1/InventoryLineFontFixerPolicyTests.cs`

- [ ] **Step 1: Write policy tests for gated refresh**

Update `InventoryLineRenderProbePatchTests.InventoryLineTranslationPatch_RefreshesFallbackFontAfterFinalItemText` to expect the gated method name without relying on one-line formatting:

```csharp
Assert.That(
    source,
    Does.Contain("InventoryLineFontFixer.TryRefreshTextSkinWithFallbackFontForSetData("));
Assert.That(source, Does.Contain("instance"));
Assert.That(source, Does.Contain("itemTextSkin"));
Assert.That(source, Does.Contain("translatedDisplayName"));
```

Update `InventoryLineRenderProbePatchTests.InventoryLineTranslationPatch_LogsOriginalTmpLifecycleAroundOwnerTextAndFontRefresh` so the order check uses the gated method name:

```csharp
var fontRefreshIndex = source.IndexOf(
    "InventoryLineFontFixer.TryRefreshTextSkinWithFallbackFontForSetData(",
    StringComparison.Ordinal);
```

Add a test proving `InventoryLineRenderProbePatch` uses the same gated path:

```csharp
[Test]
public void InventoryLineRenderProbePatch_UsesGatedSetDataFontRefresh()
{
    var sourcePath = Path.Combine(
        TestProjectPaths.GetRepositoryRoot(),
        "Mods",
        "QudJP",
        "Assemblies",
        "src",
        "Patches",
        "InventoryLineRenderProbePatch.cs");
    var source = File.ReadAllText(sourcePath);

    Assert.That(
        source,
        Does.Contain("InventoryLineFontFixer.TryApplyPrimaryFontToItemRowForSetData(__instance, data)"));
}
```

Add a test proving `InventoryLineRenderProbePatch` runs after translation postfixes:

```csharp
[Test]
public void InventoryLineRenderProbePatch_RunsAfterTranslationPostfix()
{
    var sourcePath = Path.Combine(
        TestProjectPaths.GetRepositoryRoot(),
        "Mods",
        "QudJP",
        "Assemblies",
        "src",
        "Patches",
        "InventoryLineRenderProbePatch.cs");
    var source = File.ReadAllText(sourcePath);

    Assert.That(source, Does.Contain("[HarmonyPriority(Priority.Last)]"));
}
```

In `InventoryLineFontFixerPolicyTests`, add:

```csharp
[Test]
public void SetDataRefresh_SkipsHealthySuccessfulRefreshKeys()
{
    var sourcePath = Path.Combine(
        TestProjectPaths.GetRepositoryRoot(),
        "Mods",
        "QudJP",
        "Assemblies",
        "src",
        "UI",
        "InventoryLineFontFixer.cs");
    var source = File.ReadAllText(sourcePath);
    var method = ExtractMethodBody(source, "TryRefreshTextSkinWithFallbackFontForSetData");

    Assert.That(method, Does.Contain("GetActiveItemLineRefreshKey(inventoryLineInstance)"));
    Assert.That(method, Does.Contain("HasHealthySuccessfulRefreshForCurrentKey(inventoryLineInstance, preRefreshKey)"));
    Assert.That(method, Does.Contain("RecordSuccessfulRefreshForCurrentKey(inventoryLineInstance, GetActiveItemLineRefreshKey(inventoryLineInstance))"));
}
```

- [ ] **Step 2: Run tests to verify failure**

Run:

```bash
dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj \
  --filter "FullyQualifiedName~InventoryLineRenderProbePatchTests|FullyQualifiedName~InventoryLineFontFixerPolicyTests" \
  --nologo
```

Expected: failures because the gated setData refresh methods do not exist.

- [ ] **Step 3: Implement gated setData refresh**

Add to `InventoryLineFontFixer` under `#if HAS_TMP`:

```csharp
internal static bool TryApplyPrimaryFontToItemRowForSetData(object? inventoryLineInstance, object? data)
{
    if (inventoryLineInstance is null || data is null)
    {
        return false;
    }

    if (!TryGetBooleanPropertyOrField(data, "category", out var isCategory) || isCategory)
    {
        return false;
    }

    var displayName = TryGetStringPropertyOrField(data, "displayName");
    var textSkin = ReflectionUtils.GetPropertyOrFieldValue(inventoryLineInstance, "text");
    return TryRefreshTextSkinWithFallbackFontForSetData(inventoryLineInstance, textSkin, displayName);
}

internal static bool TryRefreshTextSkinWithFallbackFontForSetData(
    object? inventoryLineInstance,
    object? textSkin,
    string? finalText)
{
    var preRefreshKey = GetActiveItemLineRefreshKey(inventoryLineInstance);
    if (HasHealthySuccessfulRefreshForCurrentKey(inventoryLineInstance, preRefreshKey))
    {
        return true;
    }

    var refreshed = TryRefreshTextSkinWithFallbackFont(textSkin, finalText);
    if (refreshed)
    {
        RecordSuccessfulRefreshForCurrentKey(
            inventoryLineInstance,
            GetActiveItemLineRefreshKey(inventoryLineInstance));
    }
    else
    {
        ForgetSuccessfulRefreshForLine(inventoryLineInstance);
    }

    return refreshed;
}
```

- [ ] **Step 4: Route setData callers through the gated method**

Add `[HarmonyPriority(Priority.Last)]` to `InventoryLineRenderProbePatch.Postfix` so the probe/font-safety postfix runs after the translation postfix has written final Japanese text and recorded the healthy refresh key:

```csharp
[HarmonyPriority(Priority.Last)]
public static void Postfix(object __instance, object data)
```

In `InventoryLineTranslationPatch.ApplyItemTranslations`, replace:

```csharp
_ = InventoryLineFontFixer.TryRefreshTextSkinWithFallbackFont(itemTextSkin, translatedDisplayName);
```

with:

```csharp
_ = InventoryLineFontFixer.TryRefreshTextSkinWithFallbackFontForSetData(
    __instance,
    itemTextSkin,
    translatedDisplayName);
```

Use the actual parameter name in this method. If needed, rename `ApplyItemTranslations(object instance, object data)` local references so the call is:

```csharp
_ = InventoryLineFontFixer.TryRefreshTextSkinWithFallbackFontForSetData(
    instance,
    itemTextSkin,
    translatedDisplayName);
```

In `InventoryLineRenderProbePatch.Postfix`, replace:

```csharp
_ = InventoryLineFontFixer.TryApplyPrimaryFontToItemRow(__instance, data);
```

with:

```csharp
_ = InventoryLineFontFixer.TryApplyPrimaryFontToItemRowForSetData(__instance, data);
```

Keep the older methods if other active-refresh paths still use them.

- [ ] **Step 5: Run focused tests**

Run the command from Step 2.

Expected: all selected tests pass.

## Task 5: Integration Validation And Runtime Evidence

**Files:**
- No new files expected unless runtime evidence is captured under an existing docs workflow.

- [ ] **Step 1: Run static/build gates**

Run:

```bash
just build
just test-l1
just test-l2
just test-l2g
```

Expected: all pass.

- [ ] **Step 2: Run targeted filtered tests for the touched surface**

Run:

```bash
dotnet test Mods/QudJP/Assemblies/QudJP.Tests/QudJP.Tests.csproj \
  --filter "FullyQualifiedName~QudMenuBottomContextTranslationPatchTests|FullyQualifiedName~PopupPickOptionTranslationPatchTests|FullyQualifiedName~PopupTranslationPatchTests|FullyQualifiedName~PopupRouteHandoffTranslationTests|FullyQualifiedName~Issue201StatusScreensBatch2Tests|FullyQualifiedName~InventoryLineRenderProbePatchTests|FullyQualifiedName~InventoryLineFontFixerPolicyTests|FullyQualifiedName~ReflectionUtilsTests|FullyQualifiedName~TargetMethodResolutionTests" \
  --nologo
```

Expected: all pass.

- [ ] **Step 3: Runtime check**

Run:

```bash
just deploy-dev
```

Manual verification in Caves of Qud 1.0.4:

1. Open inventory.
2. Select an item that has many actions.
3. Open and close the action menu repeatedly.
4. Move selection across several inventory rows.
5. Confirm action labels are displayed in Japanese where dictionaries cover them.
6. Confirm tutorial/control IDs are not broken by verifying no menu action disappears and no tutorial step requiring English `simpleText` stalls.
7. Inspect fresh logs under `~/Library/Logs/Freehold Games/CavesOfQud/` for new QudJP errors.

Expected: no new QudJP exceptions; visible row text remains translated; repeated context-menu open/close feels smoother than the pre-change baseline.

## Review Gate

Before implementation starts, this plan must pass an independent review cycle:

1. Dispatch a fresh independent reviewer with no hidden session assumptions.
2. Reviewer must classify findings as Critical, Important, or Minor.
3. Fix every Critical and Important issue in this plan.
4. Repeat the review until the reviewer returns `APPROVED`.
5. Only then start implementation.

## Self-Review

- Spec coverage: includes non-probe mitigations, bottom context menu open/close work, inventory row translation re-execution, reflection access, and TMP font refresh repeat work.
- Placeholder scan: no banned placeholder markers or unspecified test steps remain.
- Type consistency: planned method names are stable across tests and implementation sections:
  - `SelectableTextMenuItemTranslationPatch.ClearDisplayTranslationCacheForTests`
  - `SelectableTextMenuItemTranslationPatch.GetDisplayTranslationCacheCountForTests`
  - `InventoryLineTranslationPatch.ClearTranslationCachesForTests`
  - `InventoryLineTranslationPatch.GetTranslationCacheCountForTests`
  - `ReflectionUtils.ClearAccessorCacheForTests`
  - `ReflectionUtils.GetAccessorCacheCountForTests`
  - `InventoryLineFontFixer.TryRefreshTextSkinWithFallbackFontForSetData`
  - `InventoryLineFontFixer.TryApplyPrimaryFontToItemRowForSetData`
