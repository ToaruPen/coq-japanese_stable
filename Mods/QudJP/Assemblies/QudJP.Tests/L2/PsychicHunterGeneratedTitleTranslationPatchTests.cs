using QudJP.Patches;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class PsychicHunterGeneratedTitleTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        DynamicTextObservability.ResetForTests();
    }

    [Test]
    public void TranslateExpandedText_RecordsObservedFragmentRoute()
    {
        var result = PsychicHunterGeneratedTitleTranslationPatch.TranslateExpandedText("stalker");

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo("追跡者"));
            Assert.That(HitCount("ExpandString"), Is.EqualTo(1));
        });
    }

    [Test]
    public void AddTranslatedTitle_TranslatesAndRecordsTitleRoute()
    {
        var titles = CreateDummyTitles();

        PsychicHunterGeneratedTitleTranslationPatch.AddTranslatedTitle(titles, "esper assassin", -5);

        Assert.Multiple(() =>
        {
            Assert.That(titles.Entries, Is.EqualTo(new[] { "エスパーの暗殺者|-5" }));
            Assert.That(HitCount("AddTitle"), Is.EqualTo(1));
        });
    }

    [Test]
    public void AddTranslatedTitle_StripsDirectMarkerWithoutObservabilityHit()
    {
        var titles = CreateDummyTitles();

        PsychicHunterGeneratedTitleTranslationPatch.AddTranslatedTitle(
            titles,
            MessageFrameTranslator.DirectTranslationMarker + "esper assassin",
            -5);

        Assert.Multiple(() =>
        {
            Assert.That(titles.Entries, Is.EqualTo(new[] { "esper assassin|-5" }));
            Assert.That(HitCount("AddTitle"), Is.Zero);
        });
    }

    private static int HitCount(string route) =>
        DynamicTextObservability.GetRouteFamilyHitCountForTests(
            nameof(PsychicHunterGeneratedTitleTranslationPatch),
            nameof(PsychicHunterGeneratedTitleTranslationPatch) + "." + route);

    private static DummyTitles CreateDummyTitles()
    {
        var titles = new DummyTitles();
        titles.AddTitle("seed", 0);
        titles.Entries.Clear();
        return titles;
    }

    private sealed class DummyTitles
    {
        public List<string> Entries { get; } = [];

        public void AddTitle(string title, int order)
        {
            Entries.Add(title + "|" + order);
        }
    }
}
