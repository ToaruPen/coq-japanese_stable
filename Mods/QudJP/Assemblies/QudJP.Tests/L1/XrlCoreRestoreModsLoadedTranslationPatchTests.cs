using QudJP.Patches;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
[NonParallelizable]
public sealed class XrlCoreRestoreModsLoadedTranslationPatchTests
{
    [Test]
    public void TranslateLiteralForTests_LeavesUnknownLiteralUnchanged()
    {
        Assert.That(
            XrlCoreRestoreModsLoadedTranslationPatch.TranslateLiteralForTests("unrelated"),
            Is.EqualTo("unrelated"));
    }

    [Test]
    public void TranslateLiteralForTests_HandlesEmptyColorAndDirectMarkedLiterals()
    {
        Assert.Multiple(() =>
        {
            Assert.That(XrlCoreRestoreModsLoadedTranslationPatch.TranslateLiteralForTests(string.Empty), Is.Empty);
            Assert.That(
                XrlCoreRestoreModsLoadedTranslationPatch.TranslateLiteralForTests("{{red|Incomplete Mod Configuration}}"),
                Is.EqualTo("{{red|不完全なMod構成}}"));
            Assert.That(
                XrlCoreRestoreModsLoadedTranslationPatch.TranslateLiteralForTests(
                    MessageFrameTranslator.MarkDirectTranslation("Mod Configuration Differs")),
                Is.EqualTo("Mod Configuration Differs"));
        });
    }
}
