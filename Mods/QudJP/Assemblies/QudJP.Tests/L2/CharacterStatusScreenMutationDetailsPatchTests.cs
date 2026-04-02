using System.Reflection;
using System.Text;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class CharacterStatusScreenMutationDetailsPatchTests
{
    private string tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-characterstatus-mutationdetails-l2", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(tempDirectory);
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();
        DummyCharacterStatusMutationScreen.ResetDefaults();
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();
        DummyCharacterStatusMutationScreen.ResetDefaults();

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    public void Postfix_TranslatesMutationDetails_WhenPatched()
    {
        WriteDictionary(
            ("mutation:Force Wall", "力の壁を生み出し、9マス連続の力場で敵を遮る。\n\n飛び道具は壁を通過させられる。"),
            ("mutation:Force Wall:rank:1", "9マス分の連続した固定力場を作る。\n持続: 16ラウンド\nクールダウン: 100ラウンド\n力場越しに飛び道具を撃てる。"));

        var harmonyId = $"qudjp.tests.{Guid.NewGuid():N}";
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyCharacterStatusMutationScreen), nameof(DummyCharacterStatusMutationScreen.HandleHighlightMutation)),
                postfix: new HarmonyMethod(RequireMethod(typeof(CharacterStatusScreenMutationDetailsPatch), nameof(CharacterStatusScreenMutationDetailsPatch.Postfix))));

            var screen = new DummyCharacterStatusMutationScreen();
            screen.HandleHighlightMutation(new DummyCharacterMutationLineData
            {
                mutation = new DummyCharacterMutation
                {
                    Name = "ForceWall",
                    DisplayName = "Force Wall",
                    Level = 1,
                },
            });

            Assert.That(
                screen.mutationsDetails.Text,
                Is.EqualTo("力の壁を生み出し、9マス連続の力場で敵を遮る。\n\n飛び道具は壁を通過させられる。\n\n9マス分の連続した固定力場を作る。\n持続: 16ラウンド\nクールダウン: 100ラウンド\n力場越しに飛び道具を撃てる。"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Postfix_RecordsMutationDetailsOwnerRouteTransform_WithoutUITextSkinSinkObservation_WhenPatched()
    {
        WriteDictionary(
            ("mutation:Force Wall", "力の壁を生み出し、9マス連続の力場で敵を遮る。\n\n飛び道具は壁を通過させられる。"),
            ("mutation:Force Wall:rank:1", "9マス分の連続した固定力場を作る。\n持続: 16ラウンド\nクールダウン: 100ラウンド\n力場越しに飛び道具を撃てる。"));

        var harmonyId = $"qudjp.tests.{Guid.NewGuid():N}";
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyCharacterStatusMutationScreen), nameof(DummyCharacterStatusMutationScreen.HandleHighlightMutation)),
                postfix: new HarmonyMethod(RequireMethod(typeof(CharacterStatusScreenMutationDetailsPatch), nameof(CharacterStatusScreenMutationDetailsPatch.Postfix))));

            var screen = new DummyCharacterStatusMutationScreen();
            screen.HandleHighlightMutation(new DummyCharacterMutationLineData
            {
                mutation = new DummyCharacterMutation
                {
                    Name = "ForceWall",
                    DisplayName = "Force Wall",
                    Level = 1,
                },
            });

            const string source = "You generate a wall of force...\n\n9 contiguous stationary force fields.";
            Assert.Multiple(() =>
            {
                Assert.That(
                    screen.mutationsDetails.Text,
                    Is.EqualTo("力の壁を生み出し、9マス連続の力場で敵を遮る。\n\n飛び道具は壁を通過させられる。\n\n9マス分の連続した固定力場を作る。\n持続: 16ラウンド\nクールダウン: 100ラウンド\n力場越しに飛び道具を撃てる。"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(CharacterStatusScreenTranslationPatch),
                        "CharacterStatus.MutationDetails"),
                    Is.GreaterThan(0));
                Assert.That(
                    SinkObservation.GetHitCountForTests(
                        nameof(UITextSkinTranslationPatch),
                        nameof(CharacterStatusScreenTranslationPatch),
                        SinkObservation.ObservationOnlyDetail,
                        source,
                        source),
                    Is.EqualTo(0));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Postfix_TranslatesMutationOwnerFields_WhenPatched()
    {
        WriteDictionary(
            ("Force Wall", "力の壁"),
            ("RANK", "ランク"),
            ("Mental Mutation", "精神変異"),
            ("Buy Mutation", "変異を取得"),
            ("Show Effects", "効果を表示"),
            ("mutation:Force Wall", "力の壁を生み出し、9マス連続の力場で敵を遮る。"),
            ("mutation:Force Wall:rank:1", "9マス分の連続した固定力場を作る。"));

        var harmonyId = $"qudjp.tests.{Guid.NewGuid():N}";
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyCharacterStatusMutationScreen), nameof(DummyCharacterStatusMutationScreen.HandleHighlightMutation)),
                postfix: new HarmonyMethod(RequireMethod(typeof(CharacterStatusScreenMutationDetailsPatch), nameof(CharacterStatusScreenMutationDetailsPatch.Postfix))));

            var screen = new DummyCharacterStatusMutationScreen();
            screen.HandleHighlightMutation(new DummyCharacterMutationLineData
            {
                mutation = new DummyCharacterMutation
                {
                    Name = "ForceWall",
                    DisplayName = "Force Wall",
                    Level = 1,
                },
            });

            Assert.Multiple(() =>
            {
                Assert.That(screen.mutationNameText.Text, Is.EqualTo("{{B|力の壁}}"));
                Assert.That(screen.mutationRankText.Text, Is.EqualTo("{{G|ランク 1/10}}"));
                Assert.That(screen.mutationTypeText.Text, Is.EqualTo("{{c|[精神変異]}}"));
                Assert.That(DummyCharacterStatusMutationScreen.BUY_MUTATION.Description, Is.EqualTo("変異を取得"));
                Assert.That(DummyCharacterStatusMutationScreen.BUY_MUTATION.KeyDescription, Is.EqualTo("変異を取得"));
                Assert.That(DummyCharacterStatusMutationScreen.SHOW_EFFECTS.Description, Is.EqualTo("効果を表示"));
                Assert.That(DummyCharacterStatusMutationScreen.SHOW_EFFECTS.KeyDescription, Is.EqualTo("効果を表示"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(CharacterStatusScreenTranslationPatch),
                        "CharacterStatus.ExactLookup"),
                    Is.GreaterThan(0));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(CharacterStatusScreenTranslationPatch),
                        "CharacterStatus.Rank"),
                    Is.GreaterThan(0));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(CharacterStatusScreenTranslationPatch),
                        "CharacterStatus.MutationType"),
                    Is.GreaterThan(0));
                Assert.That(
                    SinkObservation.GetHitCountForTests(
                        nameof(UITextSkinTranslationPatch),
                        nameof(CharacterStatusScreenTranslationPatch),
                        SinkObservation.ObservationOnlyDetail,
                        "Force Wall",
                        "Force Wall"),
                    Is.EqualTo(0));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Postfix_TranslatesStaticMenuOptions_WhenElementIsNull()
    {
        WriteDictionary(
            ("Buy Mutation", "変異を取得"),
            ("Show Effects", "効果を表示"));

        var harmonyId = $"qudjp.tests.{Guid.NewGuid():N}";
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyCharacterStatusMutationScreen), nameof(DummyCharacterStatusMutationScreen.HandleHighlightMutation)),
                postfix: new HarmonyMethod(RequireMethod(typeof(CharacterStatusScreenMutationDetailsPatch), nameof(CharacterStatusScreenMutationDetailsPatch.Postfix))));

            var screen = new DummyCharacterStatusMutationScreen();
            screen.HandleHighlightMutation(null);

            Assert.Multiple(() =>
            {
                Assert.That(DummyCharacterStatusMutationScreen.BUY_MUTATION.Description, Is.EqualTo("変異を取得"));
                Assert.That(DummyCharacterStatusMutationScreen.BUY_MUTATION.KeyDescription, Is.EqualTo("変異を取得"));
                Assert.That(DummyCharacterStatusMutationScreen.SHOW_EFFECTS.Description, Is.EqualTo("効果を表示"));
                Assert.That(DummyCharacterStatusMutationScreen.SHOW_EFFECTS.KeyDescription, Is.EqualTo("効果を表示"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void TryTranslateMutationDetails_TranslatesComparisonFamily_WhenAllRequiredBlocksExist()
    {
        WriteDictionary(
            ("mutation:Force Wall", "力の壁を生み出し、9マス連続の力場で敵を遮る。"),
            ("mutation:Force Wall:rank:1", "9マス分の連続した固定力場を作る。"),
            ("mutation:Force Wall:rank:2", "10マス分の連続した固定力場を作る。"),
            ("This rank", "現在ランク"),
            ("Next rank", "次ランク"));

        var changed = CharacterStatusScreenTextTranslator.TryTranslateMutationDetails(
            new DummyCharacterMutation { Name = "ForceWall", DisplayName = "Force Wall", Level = 1 },
            "You generate a wall of force.\n\n{{w|This rank}}:\n9 contiguous stationary force fields.\n\n{{w|Next rank}}:\n10 contiguous stationary force fields.",
            nameof(CharacterStatusScreenTranslationPatch),
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(changed, Is.True);
            Assert.That(
                translated,
                Is.EqualTo("力の壁を生み出し、9マス連続の力場で敵を遮る。\n\n{{w|現在ランク}}:\n9マス分の連続した固定力場を作る。\n\n{{w|次ランク}}:\n10マス分の連続した固定力場を作る。"));
        });
    }

    [Test]
    public void TryTranslateMutationDetails_PassesThroughComparisonFamily_WhenAnyRequiredBlockIsMissing()
    {
        const string source = "You generate a wall of force.\n\n{{w|This rank}}:\n9 contiguous stationary force fields.\n\n{{w|Next rank}}:\n10 contiguous stationary force fields.";

        WriteDictionary(
            ("mutation:Force Wall", "力の壁を生み出し、9マス連続の力場で敵を遮る。"),
            ("mutation:Force Wall:rank:1", "9マス分の連続した固定力場を作る。"),
            ("This rank", "現在ランク"),
            ("Next rank", "次ランク"));

        var changed = CharacterStatusScreenTextTranslator.TryTranslateMutationDetails(
            new DummyCharacterMutation { Name = "ForceWall", DisplayName = "Force Wall", Level = 1 },
            source,
            nameof(CharacterStatusScreenTranslationPatch),
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(changed, Is.False);
            Assert.That(translated, Is.EqualTo(source));
        });
    }

    [Test]
    public void TryTranslateMutationDetails_UsesStableMutationEntryName_WhenDisplayNameIsLocalized()
    {
        WriteDictionary(
            ("mutation:Acid Slime Glands", "酸を吐き出す。"),
            ("mutation:Acid Slime Glands:rank:3", "酸の勢いが増す。"));

        var changed = CharacterStatusScreenTextTranslator.TryTranslateMutationDetails(
            new DummyCharacterMutation
            {
                Name = "AcidSlimeGlands",
                EntryName = "Acid Slime Glands",
                DisplayName = "酸腺",
                Level = 3,
            },
            "Spit acid.\n\nStronger acid.",
            nameof(CharacterStatusScreenTranslationPatch),
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(changed, Is.True);
            Assert.That(translated, Is.EqualTo("酸を吐き出す。\n\n酸の勢いが増す。"));
        });
    }

    private static MethodInfo RequireMethod(Type type, string methodName)
    {
        return AccessTools.Method(type, methodName)
            ?? throw new InvalidOperationException($"Method not found: {type.FullName}.{methodName}");
    }

    private void WriteDictionary(params (string key, string text)[] entries)
    {
        var builder = new StringBuilder();
        builder.Append('{');
        builder.Append("\"entries\":[");

        for (var index = 0; index < entries.Length; index++)
        {
            if (index > 0)
            {
                builder.Append(',');
            }

            builder.Append("{\"key\":\"");
            builder.Append(EscapeJson(entries[index].key));
            builder.Append("\",\"text\":\"");
            builder.Append(EscapeJson(entries[index].text));
            builder.Append("\"}");
        }

        builder.Append("]}");
        builder.AppendLine();

        var path = Path.Combine(tempDirectory, "character-status-mutation-details-l2.ja.json");
        File.WriteAllText(path, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static string EscapeJson(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\t", "\\t", StringComparison.Ordinal);
    }
}
