using System.IO;

namespace QudJP.Tests.L1;

[NUnit.Framework.TestFixture]
[NUnit.Framework.Category("L1")]
public sealed class InventoryLineFontFixerPolicyTests
{
    [NUnit.Framework.Test]
    public void ActiveInventoryLineRefresh_PreservesRuntimeTmpFontAndInstallsFallbackChain()
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
        var method = ExtractMethodBody(source, "internal static bool TryRefreshTextSkinWithFallbackFont(object? textSkin, string? finalText)");

        NUnit.Framework.Assert.That(
            method,
            NUnit.Framework.Does.Contain("FontManager.ApplyToTextWithoutImmediateRefresh(tmp);"),
            "InventoryLine text should keep the runtime TMP font/material and receive the QudJP fallback chain without forcing TMP mesh rebuild during scroller layout.");
        NUnit.Framework.Assert.That(
            method,
            NUnit.Framework.Does.Not.Contain("FontManager.ApplyToText(tmp);"),
            "FontManager.ApplyToText forces TMP mesh refresh and can collide with Unity layout rebuild while InventoryLine rows are being laid out.");
        NUnit.Framework.Assert.That(
            method,
            NUnit.Framework.Does.Not.Contain("FontManager.ForcePrimaryFont(tmp);"),
            "Forcing the primary font on InventoryLine original TMP leaves active rows with zero generated characters in runtime probes.");
    }

    [NUnit.Framework.Test]
    public void ActiveInventoryLineRefresh_UsesOverflowModeInsteadOfEllipsisTruncation()
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
        var method = ExtractMethodBody(source, "internal static bool TryRefreshTextSkinWithFallbackFont(object? textSkin, string? finalText)");

        NUnit.Framework.Assert.That(
            method,
            NUnit.Framework.Does.Contain("tmp.overflowMode = TextOverflowModes.Overflow;"),
            "Runtime comparison probes show original InventoryLine TMP stays at Ellipsis/textTruncated while the replacement renders with Overflow.");
    }

    [NUnit.Framework.Test]
    public void ActiveInventoryLinePatch_SkipsOnlyHealthySuccessfulRefreshKeys()
    {
        var patchPath = Path.Combine(
            TestProjectPaths.GetRepositoryRoot(),
            "Mods",
            "QudJP",
            "Assemblies",
            "src",
            "Patches",
            "InventoryLineActiveTextRefreshPatch.cs");
        var patchSource = File.ReadAllText(patchPath);

        NUnit.Framework.Assert.That(
            patchSource,
            NUnit.Framework.Does.Contain("InventoryLineFontFixer.GetActiveItemLineRefreshKey(__instance)"));
        NUnit.Framework.Assert.That(
            patchSource,
            NUnit.Framework.Does.Contain("InventoryLineFontFixer.HasHealthySuccessfulRefreshForCurrentKey(__instance, preRefreshKey)"));
        NUnit.Framework.Assert.That(
            patchSource,
            NUnit.Framework.Does.Contain("InventoryLineFontFixer.RecordSuccessfulRefreshForCurrentKey("));
        NUnit.Framework.Assert.That(
            patchSource,
            NUnit.Framework.Does.Contain("InventoryLineFontFixer.ForgetSuccessfulRefreshForLine(__instance)"));

        var skipIndex = patchSource.IndexOf(
            "InventoryLineFontFixer.HasHealthySuccessfulRefreshForCurrentKey(__instance, preRefreshKey)",
            System.StringComparison.Ordinal);
        var refreshIndex = patchSource.IndexOf(
            "InventoryLineFontFixer.TryRefreshActiveItemLine(__instance)",
            System.StringComparison.Ordinal);

        NUnit.Framework.Assert.That(skipIndex, NUnit.Framework.Is.GreaterThanOrEqualTo(0));
        NUnit.Framework.Assert.That(refreshIndex, NUnit.Framework.Is.GreaterThan(skipIndex));
    }

    [NUnit.Framework.Test]
    public void SuccessfulRefreshCache_RequiresLiveRenderableTmpStateBeforeSkipping()
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
        var method = ExtractMethodBody(source, "internal static bool HasHealthySuccessfulRefreshForCurrentKey");

        NUnit.Framework.Assert.That(
            method,
            NUnit.Framework.Does.Contain("HasSuccessfulRefreshForCurrentKey(inventoryLineInstance, refreshKey)"));
        NUnit.Framework.Assert.That(
            method,
            NUnit.Framework.Does.Contain("TryGetTextMeshPro(textSkin, out var tmp)"));
        NUnit.Framework.Assert.That(
            method,
            NUnit.Framework.Does.Contain("HasLiveRenderableText(tmp)"));

        var renderabilityMethod = ExtractMethodBody(source, "private static bool HasLiveRenderableText");
        NUnit.Framework.Assert.That(
            renderabilityMethod,
            NUnit.Framework.Does.Contain("tmp.textInfo.characterCount <= 0"));
        NUnit.Framework.Assert.That(
            renderabilityMethod,
            NUnit.Framework.Does.Contain("tmp.canvasRenderer"));
        NUnit.Framework.Assert.That(
            renderabilityMethod,
            NUnit.Framework.Does.Contain("canvasRenderer.cull"));
        NUnit.Framework.Assert.That(
            renderabilityMethod,
            NUnit.Framework.Does.Contain("canvasRenderer.GetAlpha() <= 0f"));
        NUnit.Framework.Assert.That(
            renderabilityMethod,
            NUnit.Framework.Does.Contain("tmp.rectTransform.rect"));
        NUnit.Framework.Assert.That(
            renderabilityMethod,
            NUnit.Framework.Does.Contain("rect.width <= 0f || rect.height <= 0f"));
        NUnit.Framework.Assert.That(
            renderabilityMethod,
            NUnit.Framework.Does.Contain("tmp.fontSharedMaterial is null"));
        NUnit.Framework.Assert.That(
            renderabilityMethod,
            NUnit.Framework.Does.Contain("UnityRuntimeCompatibility.TryGetFaceColorAlpha(tmp.fontSharedMaterial)"));
        NUnit.Framework.Assert.That(
            renderabilityMethod,
            NUnit.Framework.Does.Contain("TryGetCombinedParentCanvasGroupAlpha(tmp.transform)"));

        var canvasGroupMethod = ExtractMethodBody(source, "private static float? TryGetCombinedParentCanvasGroupAlpha");
        NUnit.Framework.Assert.That(
            canvasGroupMethod,
            NUnit.Framework.Does.Contain("current.GetComponent(\"CanvasGroup\")"));
        NUnit.Framework.Assert.That(
            canvasGroupMethod,
            NUnit.Framework.Does.Contain("\"alpha\""));
        NUnit.Framework.Assert.That(
            canvasGroupMethod,
            NUnit.Framework.Does.Contain("combinedAlpha = (combinedAlpha ?? 1f) * alpha;"));
        NUnit.Framework.Assert.That(
            canvasGroupMethod,
            NUnit.Framework.Does.Not.Contain("return alpha;"));
    }

    [NUnit.Framework.Test]
    public void SuccessfulRefreshCache_IsBoundedAndForgetsInactiveOrKeylessLines()
    {
        var patchPath = Path.Combine(
            TestProjectPaths.GetRepositoryRoot(),
            "Mods",
            "QudJP",
            "Assemblies",
            "src",
            "Patches",
            "InventoryLineActiveTextRefreshPatch.cs");
        var patchSource = File.ReadAllText(patchPath);
        var sourcePath = Path.Combine(
            TestProjectPaths.GetRepositoryRoot(),
            "Mods",
            "QudJP",
            "Assemblies",
            "src",
            "UI",
            "InventoryLineFontFixer.cs");
        var source = File.ReadAllText(sourcePath);

        NUnit.Framework.Assert.Multiple(() =>
        {
            NUnit.Framework.Assert.That(
                patchSource,
                NUnit.Framework.Does.Contain("if (!isActiveItemLine || hasActiveReplacement)"));
            NUnit.Framework.Assert.That(
                patchSource,
                NUnit.Framework.Does.Contain("InventoryLineFontFixer.ForgetSuccessfulRefreshForLine(__instance);"));
            NUnit.Framework.Assert.That(
                patchSource,
                NUnit.Framework.Does.Contain("string.IsNullOrEmpty(preRefreshKey)"));
            NUnit.Framework.Assert.That(
                source,
                NUnit.Framework.Does.Contain("MaxSuccessfulRefreshCacheEntries"));
            NUnit.Framework.Assert.That(
                source,
                NUnit.Framework.Does.Contain("SuccessfulRefreshCacheTtlTicks"));
            NUnit.Framework.Assert.That(
                source,
                NUnit.Framework.Does.Contain("CleanupSuccessfulRefreshCacheIfNeeded();"));
        });
    }

    [NUnit.Framework.Test]
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
        var method = ExtractMethodBody(source, "internal static bool TryRefreshTextSkinWithFallbackFontForSetData");

        NUnit.Framework.Assert.That(method, NUnit.Framework.Does.Contain("GetActiveItemLineRefreshKey(inventoryLineInstance)"));
        NUnit.Framework.Assert.That(method, NUnit.Framework.Does.Contain("HasHealthySuccessfulRefreshForCurrentKey(inventoryLineInstance, preRefreshKey)"));
        NUnit.Framework.Assert.That(method, NUnit.Framework.Does.Contain("RecordSuccessfulRefreshForCurrentKey("));
        NUnit.Framework.Assert.That(method, NUnit.Framework.Does.Contain("GetActiveItemLineRefreshKey(inventoryLineInstance)"));
    }

    [NUnit.Framework.Test]
    public void SetDataRefresh_EmitsDevTimingForHeavyRefreshAndCacheSkips()
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
        var setDataMethod = ExtractMethodBody(source, "internal static bool TryRefreshTextSkinWithFallbackFontForSetData");
        var heavyRefreshMethod = ExtractMethodBody(source, "internal static bool TryRefreshTextSkinWithFallbackFont(object? textSkin, string? finalText)");

        NUnit.Framework.Assert.Multiple(() =>
        {
            NUnit.Framework.Assert.That(
                setDataMethod,
                NUnit.Framework.Does.Contain("LogRefreshTimingSkip("),
                "Cache hits should be visible so runtime evidence can separate unavoidable setData volume from avoidable heavy refresh work.");
            NUnit.Framework.Assert.That(
                heavyRefreshMethod,
                NUnit.Framework.Does.Contain("LogRefreshTimingProbe("),
                "Heavy refresh should emit a bounded dev probe with timing breakdowns.");
            NUnit.Framework.Assert.That(
                source,
                NUnit.Framework.Does.Contain("InventoryLineFontRefreshTiming/v1"));
            NUnit.Framework.Assert.That(
                source,
                NUnit.Framework.Does.Contain("InventoryLineFontRefreshSkip/v1"));
            NUnit.Framework.Assert.That(
                source,
                NUnit.Framework.Does.Contain("force_canvas_ms="));
            NUnit.Framework.Assert.That(
                source,
                NUnit.Framework.Does.Contain("force_mesh_ms="));
            NUnit.Framework.Assert.That(
                source,
                NUnit.Framework.Does.Contain("canvas_update_mode="));
            NUnit.Framework.Assert.That(
                source,
                NUnit.Framework.Does.Contain("#if QUDJP_DEV_BUILD"));
            NUnit.Framework.Assert.That(
                source,
                NUnit.Framework.Does.Contain("RuntimeDiagnostics.VerboseProbesEnabled"));
        });
    }

    [NUnit.Framework.Test]
    public void HeavyRefresh_DoesNotForceCanvasRebuildFromInventoryRows()
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
        var heavyRefreshMethod = ExtractMethodBody(source, "internal static bool TryRefreshTextSkinWithFallbackFont(object? textSkin, string? finalText)");

        NUnit.Framework.Assert.Multiple(() =>
        {
            NUnit.Framework.Assert.That(
                heavyRefreshMethod,
                NUnit.Framework.Does.Not.Contain("TryForceUpdateCanvasesOncePerFrame()"),
                "Inventory row refresh runs during FrameworkScroller layout; forcing canvases here can re-enter Unity layout rebuild.");
            NUnit.Framework.Assert.That(
                source,
                NUnit.Framework.Does.Not.Contain("ForceUpdateCanvases();"),
                "InventoryLineFontFixer must not trigger Unity's global canvas rebuild.");
            NUnit.Framework.Assert.That(
                heavyRefreshMethod,
                NUnit.Framework.Does.Not.Contain("InvokeIfPresent(textSkin, \"Apply\")"),
                "UITextSkin.Apply mutates TMP text and font state; InventoryLine refresh should not call it from the scroller layout hot path.");
            NUnit.Framework.Assert.That(
                heavyRefreshMethod,
                NUnit.Framework.Does.Not.Contain("tmp.havePropertiesChanged = true"),
                "InventoryLine refresh should not explicitly mark TMP properties dirty while Unity is rebuilding layouts.");
            NUnit.Framework.Assert.That(
                heavyRefreshMethod,
                NUnit.Framework.Does.Not.Contain("SetAllDirty"),
                "Inventory row refresh runs inside scroller layout and must not register TMP layout rebuilds.");
            NUnit.Framework.Assert.That(
                heavyRefreshMethod,
                NUnit.Framework.Does.Not.Contain("SetLayoutDirty"),
                "Inventory row refresh must not dirty layout while VisibleWindowScroller is laying out children.");
            NUnit.Framework.Assert.That(
                heavyRefreshMethod,
                NUnit.Framework.Does.Not.Contain("ForceMeshUpdate"),
                "Inventory row refresh must not synchronously rebuild TMP meshes during Unity layout.");
        });
    }

    [NUnit.Framework.Test]
    public void HeavyRefresh_ReportsLiveTextWithoutSynchronousMeshOrLayoutRebuild()
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
        var method = ExtractMethodBody(source, "internal static bool TryRefreshTextSkinWithFallbackFont(object? textSkin, string? finalText)");

        NUnit.Framework.Assert.Multiple(() =>
        {
            NUnit.Framework.Assert.That(method, NUnit.Framework.Does.Contain("var refreshed = HasLiveRenderableText(tmp);"));
            NUnit.Framework.Assert.That(
                method,
                NUnit.Framework.Does.Contain("canvasUpdateMode = refreshed ? \"live_text\" : \"deferred_until_unity_layout\";"));
            NUnit.Framework.Assert.That(method, NUnit.Framework.Does.Not.Contain("tmp.ForceMeshUpdate"));
        });
    }

    private static string ExtractMethodBody(string source, string methodName)
    {
        var methodIndex = source.IndexOf(methodName, System.StringComparison.Ordinal);
        NUnit.Framework.Assert.That(methodIndex, NUnit.Framework.Is.GreaterThanOrEqualTo(0));

        var openBraceIndex = source.IndexOf('{', methodIndex);
        NUnit.Framework.Assert.That(openBraceIndex, NUnit.Framework.Is.GreaterThanOrEqualTo(0));

        var depth = 0;
        for (var index = openBraceIndex; index < source.Length; index++)
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
                    return source.Substring(openBraceIndex, index - openBraceIndex + 1);
                }
            }
        }

        NUnit.Framework.Assert.Fail($"Could not extract method body for {methodName}.");
        return string.Empty;
    }
}
