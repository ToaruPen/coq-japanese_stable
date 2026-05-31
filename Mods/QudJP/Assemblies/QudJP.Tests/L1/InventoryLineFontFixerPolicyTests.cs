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
            NUnit.Framework.Does.Contain("FontManager.ApplyToText(tmp);"),
            "InventoryLine text should keep the runtime TMP font/material and receive the QudJP fallback chain.");
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
    public void HeavyRefresh_BatchesForceUpdateCanvasesOncePerUnityFrame()
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
        var batchingMethod = ExtractMethodBody(source, "private static bool TryForceUpdateCanvasesOncePerFrame");

        NUnit.Framework.Assert.Multiple(() =>
        {
            NUnit.Framework.Assert.That(
                heavyRefreshMethod,
                NUnit.Framework.Does.Contain("TryForceUpdateCanvasesOncePerFrame()"),
                "Inventory row refresh should not call ForceUpdateCanvases directly for every row.");
            NUnit.Framework.Assert.That(
                heavyRefreshMethod,
                NUnit.Framework.Does.Not.Contain("ForceUpdateCanvases();"),
                "The heavy canvas update must be behind the per-frame gate.");
            NUnit.Framework.Assert.That(
                batchingMethod,
                NUnit.Framework.Does.Contain("Time.frameCount"));
            NUnit.Framework.Assert.That(
                batchingMethod,
                NUnit.Framework.Does.Contain("lastForceUpdateCanvasesFrame"));
            NUnit.Framework.Assert.That(
                batchingMethod,
                NUnit.Framework.Does.Contain("return false;"));
            NUnit.Framework.Assert.That(
                batchingMethod,
                NUnit.Framework.Does.Contain("ForceUpdateCanvases();"));
            NUnit.Framework.Assert.That(
                batchingMethod,
                NUnit.Framework.Does.Contain("return true;"));
        });
    }

    [NUnit.Framework.Test]
    public void HeavyRefresh_TriesMeshBeforeCanvasForceAndRetriesOnlyAfterZeroCharacters()
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

        var firstMeshIndex = method.IndexOf(
            "tmp.ForceMeshUpdate(ignoreActiveState: true, forceTextReparsing: true);",
            System.StringComparison.Ordinal);
        var canvasRetryIndex = method.IndexOf(
            "TryForceUpdateCanvasesOncePerFrame()",
            System.StringComparison.Ordinal);
        var zeroCharacterBranchIndex = method.IndexOf(
            "if (!refreshed)",
            System.StringComparison.Ordinal);
        var secondMeshIndex = method.IndexOf(
            "tmp.ForceMeshUpdate(ignoreActiveState: true, forceTextReparsing: true);",
            firstMeshIndex + 1,
            System.StringComparison.Ordinal);

        NUnit.Framework.Assert.Multiple(() =>
        {
            NUnit.Framework.Assert.That(firstMeshIndex, NUnit.Framework.Is.GreaterThanOrEqualTo(0));
            NUnit.Framework.Assert.That(canvasRetryIndex, NUnit.Framework.Is.GreaterThan(firstMeshIndex));
            NUnit.Framework.Assert.That(zeroCharacterBranchIndex, NUnit.Framework.Is.GreaterThan(firstMeshIndex));
            NUnit.Framework.Assert.That(canvasRetryIndex, NUnit.Framework.Is.GreaterThan(zeroCharacterBranchIndex));
            NUnit.Framework.Assert.That(secondMeshIndex, NUnit.Framework.Is.GreaterThan(canvasRetryIndex));
            NUnit.Framework.Assert.That(
                method,
                NUnit.Framework.Does.Contain("canvasUpdateMode = \"not_needed\";"));
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
