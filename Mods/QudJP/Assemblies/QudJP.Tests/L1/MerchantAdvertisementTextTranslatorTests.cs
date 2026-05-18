using QudJP.Patches;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
public sealed class MerchantAdvertisementTextTranslatorTests
{
    [TestCase(
        "Come to {{|the chrome market}} for the highest quality wares.\n\nLocated 5 parasangs north of Joppa.",
        "{{|the chrome market}}へどうぞ。最高品質の商品を取りそろえています。\n\n所在地：5 parasangs north of Joppa。")]
    [TestCase(
        "The finest goods at {{|the chrome market}}.\n\nTravel 5 parasangs north of Joppa.",
        "最高の商品は{{|the chrome market}}で。\n\n道順：5 parasangs north of Joppa。")]
    [TestCase(
        "Come!\n\n5 parasangs north of Joppa.",
        "お越しください！\n\n5 parasangs north of Joppa。")]
    public void TryTranslate_TranslatesMerchantAdvertisementFrames(string source, string expected)
    {
        var translated = MerchantAdvertisementTextTranslator.TryTranslate(source, out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(result, Is.EqualTo(expected));
        });
    }

    [Test]
    public void TryTranslate_StripsDirectMarkerWithoutRetranslating()
    {
        var source = MessageFrameTranslator.DirectTranslationMarker + "Come!\n\n5 parasangs north of Joppa.";

        var translated = MerchantAdvertisementTextTranslator.TryTranslate(source, out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.False);
            Assert.That(result, Is.EqualTo("Come!\n\n5 parasangs north of Joppa."));
        });
    }

    [Test]
    public void TryTranslateBookTitle_TranslatesAdvertisementPrefixAndStripsEmbeddedMarker()
    {
        var source = "advertisement for "
            + MessageFrameTranslator.DirectTranslationMarker
            + "{{M|クユラミルの蒸留所, 伝説の樹液商}}";

        var translated = MerchantAdvertisementTextTranslator.TryTranslateBookTitle(source, out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(result, Is.EqualTo("{{M|クユラミルの蒸留所, 伝説の樹液商}}の広告"));
        });
    }

    [TestCase("")]
    [TestCase(null)]
    [TestCase("A merchant note.")]
    public void TryTranslate_LeavesUnknownOrEmptyText(string? source)
    {
        var translated = MerchantAdvertisementTextTranslator.TryTranslate(source, out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.False);
            Assert.That(result, Is.EqualTo(source ?? string.Empty));
        });
    }
}
