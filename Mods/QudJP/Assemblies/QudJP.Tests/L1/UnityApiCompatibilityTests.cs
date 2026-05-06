using System.IO;
using System.Text.RegularExpressions;

namespace QudJP.Tests.L1;

[NUnit.Framework.TestFixture]
[NUnit.Framework.Category("L1")]
public sealed class UnityApiCompatibilityTests
{
    private const RegexOptions SourceRegexOptions = RegexOptions.CultureInvariant;

    [NUnit.Framework.Test]
    public void RuntimeDiagnostics_AvoidDirectUnityColorAlphaAccess()
    {
        var directColorAlphaPattern = new Regex(@"\.\s*color\s*\.\s*a", SourceRegexOptions);
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
        var rectCenterTransformPattern = new Regex(
            @"TransformPoint\s*\(\s*text\s*\.\s*rectTransform\s*\.\s*rect\s*\.\s*center\s*\)",
            SourceRegexOptions);
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
            NUnit.Framework.Does.Not.Match(rectCenterTransformPattern));
    }
}
