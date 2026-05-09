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
        var method = ExtractMethodBody(source, "TryRefreshTextSkinWithFallbackFont");

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
        var method = ExtractMethodBody(source, "TryRefreshTextSkinWithFallbackFont");

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
        var method = ExtractMethodBody(source, "HasHealthySuccessfulRefreshForCurrentKey");

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
