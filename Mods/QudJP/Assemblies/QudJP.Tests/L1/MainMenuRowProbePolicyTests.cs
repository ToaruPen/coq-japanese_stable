using System.IO;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
public sealed class MainMenuRowProbePolicyTests
{
    [Test]
    public void MainMenuRowTranslationPatch_EmitsDevOnlyLegacyTextProbe()
    {
        var sourcePath = Path.Combine(
            TestProjectPaths.GetRepositoryRoot(),
            "Mods",
            "QudJP",
            "Assemblies",
            "src",
            "Patches",
            "MainMenuRowTranslationPatch.cs");
        var source = File.ReadAllText(sourcePath);

        Assert.Multiple(() =>
        {
            Assert.That(source, Does.Contain("RuntimeDiagnostics.LogVerboseProbe"));
            Assert.That(source, Does.Contain("MainMenuRowObservability.TryBuildState"));
        });
    }
}
