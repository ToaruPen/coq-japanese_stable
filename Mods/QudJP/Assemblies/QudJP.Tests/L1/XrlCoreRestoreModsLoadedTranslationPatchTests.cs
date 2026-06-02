using QudJP.Patches;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
[NonParallelizable]
public sealed class XrlCoreRestoreModsLoadedTranslationPatchTests
{
    [TestCase("Incomplete Mod Configuration", "不完全なMod構成")]
    [TestCase("", "")]
    [TestCase("Unknown mod frame", "Unknown mod frame")]
    [TestCase("{{red|Incomplete Mod Configuration}}", "{{red|不完全なMod構成}}")]
    [TestCase("\u0001Mod Configuration Differs", "Mod Configuration Differs")]
    [TestCase("unrelated", "unrelated")]
    public void TranslateLiteralForTests_CoversRestoreModsLoadedEdgeCases(
        string source,
        string expected)
    {
        Assert.That(XrlCoreRestoreModsLoadedTranslationPatch.TranslateLiteralForTests(source), Is.EqualTo(expected));
    }
}
