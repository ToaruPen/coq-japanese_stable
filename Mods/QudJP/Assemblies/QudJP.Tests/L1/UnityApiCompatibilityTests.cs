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
        var sourcePath = Path.Combine(
            TestProjectPaths.GetRepositoryRoot(),
            "Mods",
            "QudJP",
            "Assemblies",
            "src",
            "UI",
            "TextShellReplacementRenderer.cs");
        var source = File.ReadAllText(sourcePath);

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
}
