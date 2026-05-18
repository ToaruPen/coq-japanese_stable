using QudJP.Patches;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
public sealed class VillageWallDescriptionTranslatorTests
{
    [TestCase(
        "A leather wrought from the peeled and tanned hide of glowfish was hung in a fashion inspired by spirals.",
        "グロウフィッシュの剥がしてなめした皮から作られた革が、螺旋に着想を得た様式で掛けられている。")]
    [TestCase(
        "Planks of witchwood have been cut in a layered style and bound together with asphalt and rope.",
        "ウィッチウッドの板材が層状様式に切り出され、アスファルトと縄で束ねられている。")]
    [TestCase(
        "Planks of witchwood have been cut in a layered style and bound together with asphalt and strips of livid creeper fibrous bark.",
        "ウィッチウッドの板材が層状様式に切り出され、アスファルトとリヴィドクリーパーの繊維質の樹皮の細片で束ねられている。")]
    [TestCase(
        "Planks of witchwood have been cut in a layered style and bound together with asphalt and the hide of Mamon Souldrinker.",
        "ウィッチウッドの板材が層状様式に切り出され、アスファルトとMamon Souldrinkerの皮で束ねられている。")]
    [TestCase(
        "Planks of witchwood have been cut in a layered style and bound together with asphalt and glowfish hide.",
        "ウィッチウッドの板材が層状様式に切り出され、アスファルトとグロウフィッシュの皮で束ねられている。")]
    [TestCase(
        "Crack-stuck asphalt binds together the stiff and layered bones of Mamon Souldrinker.",
        "ひび割れに詰まったアスファルトが、Mamon Souldrinkerの硬く層状な骨をつなぎ留めている。")]
    [TestCase(
        "Crack-stuck asphalt binds together the stiff and layered bones of several slaughtered glowfish.",
        "ひび割れに詰まったアスファルトが、屠られたいくつかのグロウフィッシュの硬く層状な骨をつなぎ留めている。")]
    public void TryTranslate_TranslatesVillageWallFrame(string source, string expected)
    {
        var translated = VillageWallDescriptionTranslator.TryTranslate(source, out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(result, Is.EqualTo(expected));
        });
    }

    [Test]
    public void TryTranslate_PreservesCaptureMarkup()
    {
        var source = "{{Y|Planks}} of {{g|witchwood}} have been cut in a layered style and bound together with asphalt and rope.";

        var translated = VillageWallDescriptionTranslator.TryTranslate(source, out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(result, Is.EqualTo("{{g|ウィッチウッド}}の{{Y|板材}}が層状様式に切り出され、アスファルトと縄で束ねられている。"));
        });
    }

    [TestCase("")]
    [TestCase(null)]
    [TestCase("A plain wall with no generated history.")]
    public void TryTranslate_LeavesUnknownOrEmptyText(string? source)
    {
        var translated = VillageWallDescriptionTranslator.TryTranslate(source, out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.False);
            Assert.That(result, Is.EqualTo(source ?? string.Empty));
        });
    }

    [Test]
    public void TryTranslate_LeavesMatchedFrameWithUnknownLowercaseCapture()
    {
        var source = "Planks of unknown wood have been cut in a layered style and bound together with asphalt and rope.";

        var translated = VillageWallDescriptionTranslator.TryTranslate(source, out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.False);
            Assert.That(result, Is.EqualTo(source));
        });
    }
}
