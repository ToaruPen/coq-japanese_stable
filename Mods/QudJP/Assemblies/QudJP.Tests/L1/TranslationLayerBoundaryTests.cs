namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
public sealed class TranslationLayerBoundaryTests
{
    [Test]
    public void MessageFrameTranslator_DoesNotDependOnPatchImplementationNamespace()
    {
        var sourcePath = Path.Combine(
            TestProjectPaths.GetRepositoryRoot(),
            "Mods",
            "QudJP",
            "Assemblies",
            "src",
            "Translation",
            "MessageFrameTranslator.cs");
        var source = File.ReadAllText(sourcePath);

        Assert.Multiple(() =>
        {
            Assert.That(source, Does.Not.Contain("using QudJP.Patches;"));
            Assert.That(source, Does.Not.Contain("GetDisplayNameRouteTranslator"));
        });
    }
}
