using System.Reflection;
using System.Runtime.CompilerServices;
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

        WithPatchedCreateQuest(() =>
        {
            DummyDynamicQuestFactory.NextQuest = quest;

            var result = DummyDynamicQuestFactory.CreateQuest();

            Assert.Multiple(() =>
            {
                Assert.That(result.Name, Is.EqualTo("{{&Y|ドリンクス}}が錆びた遺物を探すのを助ける"));
                Assert.That(result.StepsByID["a_locate"].Name, Is.EqualTo("錆びた遺物を探す"));
                Assert.That(result.StepsByID["a_locate"].Text, Is.EqualTo("{{|錆の井戸}}で錆びた遺物を見つける。"));
                Assert.That(result.StepsByID["b_return"].Name, Is.EqualTo("錆びた遺物をジョッパへ返す"));
                Assert.That(result.StepsByID["b_return"].Text, Is.EqualTo("錆びた遺物をジョッパへ返し、Mehmetと話す。"));
                Assert.That(HitCount("QuestName"), Is.EqualTo(1));
                Assert.That(HitCount("QuestStepName"), Is.EqualTo(2));
                Assert.That(HitCount("QuestStepText"), Is.EqualTo(2));
            });
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

        WithPatchedCreateQuest(() =>
        {
            DummyDynamicQuestFactory.NextQuest = quest;

            var result = DummyDynamicQuestFactory.CreateQuest();

            Assert.Multiple(() =>
            {
                Assert.That(result.Name, Is.EqualTo("Find the rusted relic"));
                Assert.That(result.StepsByID["a_locate"].Name, Is.EqualTo("Return to Joppa"));
                Assert.That(result.StepsByID["a_locate"].Text, Is.EqualTo("Unknown step text."));
                Assert.That(HitCount("QuestName"), Is.EqualTo(1));
                Assert.That(HitCount("QuestStepName"), Is.EqualTo(1));
                Assert.That(HitCount("QuestStepText"), Is.Zero);
            });
        });
    }

    private static void WithPatchedCreateQuest(Action action)
    {
        var harmonyId = "qudjp.tests.dynamic-quest-generated-text." + Guid.NewGuid().ToString("N");
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyDynamicQuestFactory), nameof(DummyDynamicQuestFactory.CreateQuest)),
                postfix: new HarmonyMethod(RequireMethod(
                    typeof(DynamicQuestGeneratedQuestTextTranslationPatch),
                    nameof(DynamicQuestGeneratedQuestTextTranslationPatch.Postfix),
                    typeof(object))));
            action();
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
            DummyDynamicQuestFactory.NextQuest = null;
        }
    }

    private static MethodInfo RequireMethod(Type type, string name, params Type[] parameterTypes)
    {
        var method = AccessTools.Method(type, name, parameterTypes);
        Assert.That(method, Is.Not.Null, $"{type.FullName}.{name} not found");
        return method!;
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

internal static class DummyDynamicQuestFactory
{
    public static DummyDynamicQuest? NextQuest { get; set; }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static DummyDynamicQuest CreateQuest()
    {
        return NextQuest ?? new DummyDynamicQuest();
    }
}

internal sealed class DummyDynamicQuestStep
{
    public string Name { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;
}
