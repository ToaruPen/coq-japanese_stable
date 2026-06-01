using QudJP.Patches;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
[NonParallelizable]
public sealed class DescriptionLookPopupTranslationPatchTests
{
    [Test]
    public void TranslateLiteral_LeavesUnknownEmptyAndMarkedValuesSafe()
    {
        Assert.Multiple(() =>
        {
            Assert.That(DescriptionLookPopupTranslationPatch.TranslateLiteralForTests("unknown"), Is.EqualTo("unknown"));
            Assert.That(DescriptionLookPopupTranslationPatch.TranslateLiteralForTests(string.Empty), Is.Empty);
            Assert.That(
                DescriptionLookPopupTranslationPatch.TranslateLiteralForTests("\u0001Recall {{W|S}}tory"),
                Is.EqualTo("Recall {{W|S}}tory"));
        });
    }
}
