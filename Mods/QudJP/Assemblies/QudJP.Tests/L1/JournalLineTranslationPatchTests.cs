using QudJP.Patches;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
public sealed class JournalLineTranslationPatchTests
{
    [TestCase(2, "Small", false)]
    [TestCase(1, "Small", true)]
    [TestCase(1, "Medium", false)]
    public void ShouldClipForSmallMedia_UsesCategoryAndMediaSize(
        int currentCategory,
        string sizeClass,
        bool expected)
    {
        Assert.That(
            JournalLineTranslationPatch.ShouldClipForSmallMedia(currentCategory, sizeClass),
            Is.EqualTo(expected));
    }
}
