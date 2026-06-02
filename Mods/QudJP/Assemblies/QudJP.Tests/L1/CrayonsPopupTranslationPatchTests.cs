using QudJP.Patches;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
[NonParallelizable]
public sealed class CrayonsPopupTranslationPatchTests
{
    [Test]
    public void TranslateLiteral_LeavesUnknownEmptyAndMarkedValuesSafe()
    {
        Assert.Multiple(() =>
        {
            Assert.That(CrayonsPopupTranslationPatch.TranslateLiteralForTests("unknown prompt"), Is.EqualTo("unknown prompt"));
            Assert.That(CrayonsPopupTranslationPatch.TranslateLiteralForTests(string.Empty), Is.Empty);
            Assert.That(CrayonsPopupTranslationPatch.TranslateLiteralForTests("\u0001You draw a pretty picture."), Is.EqualTo("You draw a pretty picture."));
            Assert.That(
                CrayonsPopupTranslationPatch.TranslateLiteralForTests("{{Y|You draw a pretty picture.}}"),
                Is.EqualTo("{{Y|きれいな絵を描いた。}}"));
        });
    }
}
