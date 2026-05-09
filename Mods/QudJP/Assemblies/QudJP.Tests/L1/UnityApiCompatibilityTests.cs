using System.IO;
using System.Text.RegularExpressions;

namespace QudJP.Tests.L1;

[NUnit.Framework.TestFixture]
[NUnit.Framework.Category("L1")]
public sealed class UnityApiCompatibilityTests
{
    private const RegexOptions SourceRegexOptions = RegexOptions.CultureInvariant;

    private static readonly Regex RectCenterTransformPattern = new(
        @"TransformPoint\s*\(\s*(?!UnityRuntimeCompatibility\s*\.\s*ToVector3\s*\()[\s\S]*?\.\s*rectTransform\s*\.\s*rect\s*\.\s*center\s*\)",
        SourceRegexOptions);

    private static readonly Regex DirectVector2CoordinateAccessPattern = new(
        @"\b(?:value|vector)\s*\.\s*[xy]\b",
        SourceRegexOptions);

    private static readonly Regex TooltipObjectPatternNullCheck = new(
        @"\btooltipObject\s+is\s+(?:not\s+)?null\b",
        SourceRegexOptions);

    [NUnit.Framework.Test]
    public void RuntimeDiagnostics_AvoidDirectUnityColorAlphaAccess()
    {
        var directColorAlphaPattern = new Regex(@"\.\s*color\s*\.\s*a\b", SourceRegexOptions);
        var faceColorAlphaPattern = new Regex(@"GetColor\s*\(\s*""_FaceColor""\s*\)\s*\.\s*a", SourceRegexOptions);
        var sourceRoot = Path.Combine(
            TestProjectPaths.GetRepositoryRoot(),
            "Mods",
            "QudJP",
            "Assemblies",
            "src");

        foreach (var sourcePath in Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories))
        {
            var source = File.ReadAllText(sourcePath);

            NUnit.Framework.Assert.That(
                source,
                NUnit.Framework.Does.Not.Match(directColorAlphaPattern),
                Path.GetRelativePath(TestProjectPaths.GetRepositoryRoot(), sourcePath));
            NUnit.Framework.Assert.That(
                source,
                NUnit.Framework.Does.Not.Match(faceColorAlphaPattern),
                Path.GetRelativePath(TestProjectPaths.GetRepositoryRoot(), sourcePath));
        }
    }

    [NUnit.Framework.Test]
    public void RuntimeDiagnostics_AvoidRectCenterImplicitVectorConversion()
    {
        var source = ReadUiSource("TextShellReplacementRenderer.cs");

        NUnit.Framework.Assert.That(
            source,
            NUnit.Framework.Does.Not.Match(RectCenterTransformPattern));
    }

    [NUnit.Framework.TestCase("TransformPoint(text.rectTransform.rect.center)")]
    [NUnit.Framework.TestCase("TransformPoint(original.rectTransform.rect.center)")]
    [NUnit.Framework.TestCase("TransformPoint(state.currentText.rectTransform.rect.center)")]
    [NUnit.Framework.TestCase("TransformPoint(GetText().rectTransform.rect.center)")]
    [NUnit.Framework.TestCase("TransformPoint(((TMP_Text)text).rectTransform.rect.center)")]
    [NUnit.Framework.TestCase("TransformPoint( text . rectTransform . rect . center )")]
    public void RectCenterTransformPattern_CatchesReceiverNameVariants(string source)
    {
        NUnit.Framework.Assert.That(source, NUnit.Framework.Does.Match(RectCenterTransformPattern));
    }

    [NUnit.Framework.Test]
    public void RectCenterTransformPattern_AllowsExplicitVectorConversion()
    {
        const string source = "TransformPoint(UnityRuntimeCompatibility.ToVector3(text.rectTransform.rect.center))";

        NUnit.Framework.Assert.That(source, NUnit.Framework.Does.Not.Match(RectCenterTransformPattern));
    }

    [NUnit.Framework.Test]
    public void DirectColorAlphaPattern_AllowsLongerAlphaMemberNames()
    {
        var directColorAlphaPattern = new Regex(@"\.\s*color\s*\.\s*a\b", SourceRegexOptions);

        NUnit.Framework.Assert.That("text.color.alpha", NUnit.Framework.Does.Not.Match(directColorAlphaPattern));
    }

    [NUnit.Framework.TestCase("new Vector3(value.x, value.y, 0f)")]
    [NUnit.Framework.TestCase("new Vector3(vector.x, vector.y, 0f)")]
    public void DirectVector2CoordinateAccessPattern_CatchesKnownParameterNames(string source)
    {
        NUnit.Framework.Assert.That(source, NUnit.Framework.Does.Match(DirectVector2CoordinateAccessPattern));
    }

    [NUnit.Framework.Test]
    public void RuntimeCompatibility_ToVector3AvoidsDirectVector2CoordinateAccess()
    {
        var source = ReadUiSource("UnityRuntimeCompatibility.cs");

        NUnit.Framework.Assert.That(
            source,
            NUnit.Framework.Does.Not.Match(DirectVector2CoordinateAccessPattern),
            "UnityEngine.Vector2 x/y access can compile to get_x/get_y calls that are absent in the shipped game runtime.");
    }

    [NUnit.Framework.Test]
    public void TooltipRepairer_UsesUnityNullSemanticsForTooltipObjects()
    {
        var source = ReadUiSource("TooltipTextRepairer.cs");

        NUnit.Framework.Assert.That(
            source,
            NUnit.Framework.Does.Not.Match(TooltipObjectPatternNullCheck),
            "UnityEngine.Object fake-null semantics require ==/!= null instead of pattern null checks.");
        NUnit.Framework.Assert.That(source, NUnit.Framework.Does.Contain("tooltipObject == null"));
        NUnit.Framework.Assert.That(source, NUnit.Framework.Does.Contain("tooltipObject != null"));
    }

    [NUnit.Framework.Test]
    public void TooltipRepairer_DoesNotUseInventoryTextShellReplacementForTooltipRoute()
    {
        var source = ReadUiSource("TooltipTextRepairer.cs");

        NUnit.Framework.Assert.That(
            source,
            NUnit.Framework.Does.Not.Contain("TextShellReplacementRenderer.TryRenderReplacementTexts"),
            "Tooltip repair must not reuse the InventoryLine TextShell replacement path because it can hide original tooltip TMP text.");
    }

    [NUnit.Framework.Test]
    public void TooltipRepairer_UsesDedicatedReplacementRendererForTooltipRoute()
    {
        var source = ReadUiSource("TooltipTextRepairer.cs");

        NUnit.Framework.Assert.That(
            source,
            NUnit.Framework.Does.Contain("TooltipReplacementRenderer.TryRenderReplacementTexts"),
            "Tooltip repair needs its own replacement renderer because some tooltip TMP fields do not recover through font/material refresh alone.");
    }

    [NUnit.Framework.Test]
    public void TooltipReplacementRenderer_DoesNotUseInventoryTextShellLeafContract()
    {
        var source = ReadUiSource("TooltipReplacementRenderer.cs");

        NUnit.Framework.Assert.That(source, NUnit.Framework.Does.Not.Contain("TextShellReplacementRenderer"));
        NUnit.Framework.Assert.That(source, NUnit.Framework.Does.Not.Contain("IsTextShellLeaf"));
    }

    [NUnit.Framework.Test]
    public void SelectableTextMenuItemTranslation_UsesPopupMessageLastPopupIdFallback()
    {
        var source = ReadPatchSource("SelectableTextMenuItemTranslationPatch.cs");

        NUnit.Framework.Assert.That(source, NUnit.Framework.Does.Contain("TryGetLastPopupId(popupMessageType)"));
        NUnit.Framework.Assert.That(source, NUnit.Framework.Does.Contain("\"lastPopupID\""));
    }

    [NUnit.Framework.Test]
    public void TextShellReplacementRenderer_LazilyCollectsVerboseDiagnostics()
    {
        var source = ReadUiSource("TextShellReplacementRenderer.cs");

        NUnit.Framework.Assert.That(
            source,
            NUnit.Framework.Does.Contain("var collectDiagnostics = emitDiagnostics && RuntimeDiagnostics.VerboseProbesEnabled;"));
        NUnit.Framework.Assert.That(
            source,
            NUnit.Framework.Does.Contain("var builder = collectDiagnostics ? new StringBuilder() : null;"));
        NUnit.Framework.Assert.That(
            source,
            NUnit.Framework.Does.Contain("var creationStageLog = collectDiagnostics ? new StringBuilder() : null;"));
        NUnit.Framework.Assert.That(
            source,
            NUnit.Framework.Does.Contain("private static void AppendCreationStageSnapshot(StringBuilder? builder"));
        NUnit.Framework.Assert.That(
            source,
            NUnit.Framework.Does.Contain("if (builder is null)"));
        NUnit.Framework.Assert.That(
            source,
            NUnit.Framework.Does.Not.Contain("var builder = emitDiagnostics ? new StringBuilder() : null;"));
        NUnit.Framework.Assert.That(
            source,
            NUnit.Framework.Does.Not.Contain("var creationStageLog = emitDiagnostics ? new StringBuilder() : null;"));
    }

    private static string ReadUiSource(string fileName)
    {
        return File.ReadAllText(Path.Combine(
            TestProjectPaths.GetRepositoryRoot(),
            "Mods",
            "QudJP",
            "Assemblies",
            "src",
            "UI",
            fileName));
    }

    private static string ReadPatchSource(string fileName)
    {
        return File.ReadAllText(Path.Combine(
            TestProjectPaths.GetRepositoryRoot(),
            "Mods",
            "QudJP",
            "Assemblies",
            "src",
            "Patches",
            fileName));
    }
}
