using HarmonyLib;
using QudJP.Patches;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class DynamicQuestGeneratedQuestTextTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        Translator.ResetForTests();
        ScopedDictionaryLookup.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(GetRepositoryDictionaryDirectory());
        DynamicTextObservability.ResetForTests();
    }

    [TearDown]
    public void TearDown()
    {
        ScopedDictionaryLookup.ResetForTests();
        Translator.ResetForTests();
    }

    [Test]
    public void Postfix_TranslatesQuestNameAndSteps()
    {
        var quest = new DummyDynamicQuest
        {
            Name = "Aiding {{&Y|ドリンクス}} to Find the rusted relic",
            StepsByID =
            {
                ["a_locate"] = new DummyDynamicQuestStep
                {
                    Name = "Find the rusted relic",
                    Text = "Locate the rusted relic at {{|the rust wells}}.",
                },
                ["b_return"] = new DummyDynamicQuestStep
                {
                    Name = "Return the rusted relic to Joppa",
                    Text = "Return the rusted relic to Joppa and speak with Mehmet.",
                },
            },
        };

        DynamicQuestGeneratedQuestTextTranslationPatch.Postfix(quest);

        Assert.Multiple(() =>
        {
            Assert.That(quest.Name, Is.EqualTo("{{&Y|ドリンクス}}が錆びた遺物を探すのを助ける"));
            Assert.That(quest.StepsByID["a_locate"].Name, Is.EqualTo("錆びた遺物を探す"));
            Assert.That(quest.StepsByID["a_locate"].Text, Is.EqualTo("{{|錆の井戸}}で錆びた遺物を見つける。"));
            Assert.That(quest.StepsByID["b_return"].Name, Is.EqualTo("錆びた遺物をジョッパへ返す"));
            Assert.That(quest.StepsByID["b_return"].Text, Is.EqualTo("錆びた遺物をジョッパへ返し、Mehmetと話す。"));
            Assert.That(HitCount("QuestName"), Is.EqualTo(1));
            Assert.That(HitCount("QuestStepName"), Is.EqualTo(2));
            Assert.That(HitCount("QuestStepText"), Is.EqualTo(2));
        });
    }

    [Test]
    public void Postfix_StripsDirectMarkerWithoutObservabilityHit()
    {
        var quest = new DummyDynamicQuest
        {
            Name = MessageFrameTranslator.DirectTranslationMarker + "Find the rusted relic",
            StepsByID =
            {
                ["a_locate"] = new DummyDynamicQuestStep
                {
                    Name = MessageFrameTranslator.DirectTranslationMarker + "Return to Joppa",
                    Text = "Unknown step text.",
                },
            },
        };

        DynamicQuestGeneratedQuestTextTranslationPatch.Postfix(quest);

        Assert.Multiple(() =>
        {
            Assert.That(quest.Name, Is.EqualTo("Find the rusted relic"));
            Assert.That(quest.StepsByID["a_locate"].Name, Is.EqualTo("Return to Joppa"));
            Assert.That(quest.StepsByID["a_locate"].Text, Is.EqualTo("Unknown step text."));
            Assert.That(HitCount("QuestName"), Is.Zero);
            Assert.That(HitCount("QuestStepName"), Is.Zero);
            Assert.That(HitCount("QuestStepText"), Is.Zero);
        });
    }

    private static int HitCount(string route) =>
        DynamicTextObservability.GetRouteFamilyHitCountForTests(
            nameof(DynamicQuestGeneratedQuestTextTranslationPatch),
            nameof(DynamicQuestGeneratedQuestTextTranslationPatch) + "." + route);

    private static string GetRepositoryDictionaryDirectory() =>
        Path.Combine(QudJP.Tests.L1.TestProjectPaths.GetRepositoryRoot(), "Mods", "QudJP", "Localization", "Dictionaries");
}

internal sealed class DummyDynamicQuest
{
    public string Name { get; set; } = string.Empty;

    public Dictionary<string, DummyDynamicQuestStep> StepsByID { get; } = [];
}

internal sealed class DummyDynamicQuestStep
{
    public string Name { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;
}
