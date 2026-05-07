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

    [NUnit.Framework.TestCase("new Vector3(value.x, value.y, 0f)")]
    [NUnit.Framework.TestCase("new Vector3(vector.x, vector.y, 0f)")]
    public void DirectVector2CoordinateAccessPattern_CatchesKnownParameterNames(string source)
    {
        NUnit.Framework.Assert.That(source, NUnit.Framework.Does.Match(DirectVector2CoordinateAccessPattern));
    }

    [NUnit.Framework.Test]
    public void RuntimeCompatibility_ToVector3AvoidsDirectVector2CoordinateAccess()
    {
        var sourcePath = Path.Combine(
            TestProjectPaths.GetRepositoryRoot(),
            "Mods",
            "QudJP",
            "Assemblies",
            "src",
            "UI",
            "UnityRuntimeCompatibility.cs");
        var source = File.ReadAllText(sourcePath);

        NUnit.Framework.Assert.That(
            source,
            NUnit.Framework.Does.Not.Match(DirectVector2CoordinateAccessPattern),
            "UnityEngine.Vector2 x/y access can compile to get_x/get_y calls that are absent in the shipped game runtime.");
    }
}
