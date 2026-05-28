using System.Text;
using QudJP.Patches;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
[NonParallelizable]
public sealed class CyberneticsDescriptionAssignmentTranslationPatchTests
{
    private string tempRoot = null!;
    private string dictionariesDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempRoot = Path.Combine(Path.GetTempPath(), "qudjp-cybernetics-description-assignment-l2", Guid.NewGuid().ToString("N"));
        dictionariesDirectory = Path.Combine(tempRoot, "Dictionaries");
        Directory.CreateDirectory(dictionariesDirectory);

        LocalizationAssetResolver.SetLocalizationRootForTests(tempRoot);
        Translator.SetDictionaryDirectoryForTests(dictionariesDirectory);
        ChargenStructuredTextTranslator.ResetForTests();
        DynamicTextObservability.ResetForTests();
    }

    [TearDown]
    public void TearDown()
    {
        ChargenStructuredTextTranslator.ResetForTests();
        DynamicTextObservability.ResetForTests();
        Translator.ResetForTests();
        LocalizationAssetResolver.SetLocalizationRootForTests(null);

        if (Directory.Exists(tempRoot))
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Test]
    public void TranslateMotorizedTreadsPart_TranslatesLowerBodyNameAndDescription()
    {
        var part = new DummyBodyPart
        {
            Name = "lower body",
            Description = "Lower Body",
            Manager = "MotorizedTreadsChanges",
        };

        CyberneticsDescriptionAssignmentTranslationPatch.TranslateMotorizedTreadsPart(part);

        Assert.Multiple(() =>
        {
            Assert.That(part.Name, Is.EqualTo("下半身"));
            Assert.That(part.Description, Is.EqualTo("下半身"));
            Assert.That(part.Manager, Is.EqualTo("MotorizedTreadsChanges"));
        });
    }

    [Test]
    public void TranslateMotorizedTreadsPart_PreservesUnknownAndMarkerPrefixedValues()
    {
        var part = new DummyBodyPart
        {
            Name = "unknown body",
            Description = "\u0001Lower Body",
        };

        CyberneticsDescriptionAssignmentTranslationPatch.TranslateMotorizedTreadsPart(part);

        Assert.Multiple(() =>
        {
            Assert.That(part.Name, Is.EqualTo("unknown body"));
            Assert.That(part.Description, Is.EqualTo("Lower Body"));
        });
    }

    [Test]
    public void TranslateStasisArenaDescription_TranslatesGeneratedDescription()
    {
        var evt = new DummyCyberneticsDescriptionEvent
        {
            Description = "Activated. Cooldown 50.\nPick an exclusion zone of up to 3 squares; the rest of the zone, other than the square you are in, is enveloped in stasis fields that last 10-20 turns.\nCompute power on the local lattice increases this implant's effectiveness.",
        };

        CyberneticsDescriptionAssignmentTranslationPatch.TranslateStasisArenaEvent(evt);

        Assert.That(
            evt.Description,
            Is.EqualTo("起動型。クールダウン 50。\n最大3マスの除外区域を選ぶ。現在いるマスを除くゾーンの残りは、10-20ターン持続する停滞フィールドに包まれる。\nローカル格子の計算力はこのインプラントの効果を高める。"));
    }

    [Test]
    public void TranslateStasisArenaDescription_TranslatesSingularGeneratedDescription()
    {
        var evt = new DummyCyberneticsDescriptionEvent
        {
            Description = "Activated. Cooldown 25.\nPick an exclusion zone of up to 1 square; the rest of the zone, other than the square you are in, is enveloped in stasis fields that last 8 turns.\nCompute power on the local lattice increases this implant's effectiveness.",
        };

        CyberneticsDescriptionAssignmentTranslationPatch.TranslateStasisArenaEvent(evt);

        Assert.That(
            evt.Description,
            Is.EqualTo("起動型。クールダウン 25。\n最大1マスの除外区域を選ぶ。現在いるマスを除くゾーンの残りは、8ターン持続する停滞フィールドに包まれる。\nローカル格子の計算力はこのインプラントの効果を高める。"));
    }

    [Test]
    public void TranslateStasisArenaDescription_PreservesUnknownAndStripsMarkerPrefixedValues()
    {
        var unknown = new DummyCyberneticsDescriptionEvent { Description = "Unknown cybernetics behavior." };
        var marker = new DummyCyberneticsDescriptionEvent { Description = "\u0001Activated. Cooldown 25." };

        CyberneticsDescriptionAssignmentTranslationPatch.TranslateStasisArenaEvent(unknown);
        CyberneticsDescriptionAssignmentTranslationPatch.TranslateStasisArenaEvent(marker);

        Assert.Multiple(() =>
        {
            Assert.That(unknown.Description, Is.EqualTo("Unknown cybernetics behavior."));
            Assert.That(marker.Description, Is.EqualTo("Activated. Cooldown 25."));
        });
    }

    [Test]
    public void TranslateOpticalMultiscannerEvent_TranslatesDescriptionAndAddedRule()
    {
        var evt = new DummyCyberneticsDescriptionEvent
        {
            Description = "You gain access to the precise hit point, armor, and dodge values of robotic creatures, biological creatures, and structures.\nStaircases and other up/down map transitions are always revealed to you.",
            ToAdd =
            [
                "Adds a bonus turn, and is otherwise useful, in most tinkering Sifrah games, and is useful in many social Sifrah games.",
                "unrelated behavior",
            ],
        };

        CyberneticsDescriptionAssignmentTranslationPatch.TranslateOpticalMultiscannerEvent(evt);

        Assert.Multiple(() =>
        {
            Assert.That(
                evt.Description,
                Is.EqualTo("ロボット、生物、建造物の正確なヒットポイント、アーマー値、ドッジ値を確認できる。\n階段その他の上下マップ遷移は常に明らかになる。"));
            Assert.That(
                evt.ToAdd,
                Is.EqualTo(new[]
                {
                    "ほとんどのティンカリングのシフラでボーナスターンを得て、その他にも有用になる。また、多くの社交のシフラで有用になる。",
                    "unrelated behavior",
                }));
        });
    }

    [Test]
    public void TranslateOpticalMultiscannerEvent_PreservesUnknownAndStripsMarkerPrefixedValues()
    {
        var unknown = new DummyCyberneticsDescriptionEvent
        {
            Description = "Unknown cybernetics behavior.",
            ToAdd = ["Unknown Sifrah behavior."],
        };
        var marker = new DummyCyberneticsDescriptionEvent
        {
            Description = "\u0001You gain access to the precise hit point, armor, and dodge values of robotic creatures, biological creatures, and structures.",
            ToAdd = ["\u0001Adds a bonus turn, and is otherwise useful, in most tinkering Sifrah games, and is useful in many social Sifrah games."],
        };

        CyberneticsDescriptionAssignmentTranslationPatch.TranslateOpticalMultiscannerEvent(unknown);
        CyberneticsDescriptionAssignmentTranslationPatch.TranslateOpticalMultiscannerEvent(marker);

        Assert.Multiple(() =>
        {
            Assert.That(unknown.Description, Is.EqualTo("Unknown cybernetics behavior."));
            Assert.That(unknown.ToAdd, Is.EqualTo(new[] { "Unknown Sifrah behavior." }));
            Assert.That(
                marker.Description,
                Is.EqualTo("You gain access to the precise hit point, armor, and dodge values of robotic creatures, biological creatures, and structures."));
            Assert.That(marker.ToAdd, Is.EqualTo(new[] { "Adds a bonus turn, and is otherwise useful, in most tinkering Sifrah games, and is useful in many social Sifrah games." }));
        });
    }

    [Test]
    public void TranslateSingleSkillsoftEvent_TranslatesDescriptionAndAddedSkillRule()
    {
        WriteDictionary(("Tinkering", "工匠"), ("Proselytize", "布教"));

        var evt = new DummyCyberneticsDescriptionEvent
        {
            Description = "You gain the skill Tinkering.",
            ToAdd = ["You gain the skill Proselytize."],
        };

        CyberneticsDescriptionAssignmentTranslationPatch.TranslateSingleSkillsoftEvent(evt);

        Assert.Multiple(() =>
        {
            Assert.That(evt.Description, Is.EqualTo("工匠スキルを得る。"));
            Assert.That(evt.ToAdd, Is.EqualTo(new[] { "布教スキルを得る。" }));
        });
    }

    [Test]
    public void TranslateTreeSkillsoftEvent_TranslatesDescriptionAndAddedTreeRule()
    {
        WriteDictionary(("Tactics", "戦術"), ("Cudgel", "棍棒"));

        var evt = new DummyCyberneticsDescriptionEvent
        {
            Description = "You gain access to the Tactics skill tree.",
            ToAdd = ["You gain access to the Cudgel skill tree."],
        };

        CyberneticsDescriptionAssignmentTranslationPatch.TranslateTreeSkillsoftEvent(evt);

        Assert.Multiple(() =>
        {
            Assert.That(evt.Description, Is.EqualTo("戦術スキルツリーにアクセスできる。"));
            Assert.That(evt.ToAdd, Is.EqualTo(new[] { "棍棒スキルツリーにアクセスできる。" }));
        });
    }

    [Test]
    public void TranslateSocialCoprocessorEvent_TranslatesGeneratedDescription()
    {
        var evt = new DummyCyberneticsDescriptionEvent
        {
            Description = "Whenever you perform the water ritual with a new creature, you gain an extra 125 reputation. If you install this implant after you treat with a creature for the first time, you gain 125 reputation the next time you treat with them.\nReputation costs in the water ritual are reduced by 20%.\nYou may Proselytize 1 additional creature.\nCompute power on the local lattice increases this implant's effectiveness.",
        };

        CyberneticsDescriptionAssignmentTranslationPatch.TranslateSocialCoprocessorEvent(evt);

        Assert.That(
            evt.Description,
            Is.EqualTo("新しいクリーチャーと水の儀式を行うたび、評判を追加で125得る。クリーチャーと初めて交渉した後にこのインプラントを取り付けた場合、次にその相手と交渉したときに評判を125得る。\n水の儀式での評判コストが20%減少する。\n追加で1体のクリーチャーを布教できる。\nローカル格子の計算力はこのインプラントの効果を高める。"));
    }

    [Test]
    public void TranslateTechIndexerEvent_TranslatesDescriptionAndAddedRule()
    {
        var evt = new DummyCyberneticsDescriptionEvent
        {
            Description = "You gain access to the precise hit point, armor, and dodge values of robotic creatures.",
            ToAdd = ["Adds a bonus turn, and is otherwise useful, in many tinkering Sifrah games, and is useful in some social Sifrah games involving robots."],
        };

        CyberneticsDescriptionAssignmentTranslationPatch.TranslateTechIndexerEvent(evt);

        Assert.Multiple(() =>
        {
            Assert.That(evt.Description, Is.EqualTo("ロボットの正確なヒットポイント、アーマー値、ドッジ値を確認できる。"));
            Assert.That(evt.ToAdd, Is.EqualTo(new[] { "多くのティンカリングのシフラでボーナスターンを得て、その他にも有用になる。また、ロボットに関係する一部の社交のシフラで有用になる。" }));
        });
    }

    [Test]
    public void TranslateAdditionalCyberneticsEvents_PreserveUnknownAndStripMarkerPrefixedValues()
    {
        var singleSkillsoftMarker = new DummyCyberneticsDescriptionEvent
        {
            Description = "\u0001You gain the skill Tinkering.",
            ToAdd = ["\u0001You gain the skill Proselytize."],
        };
        var treeSkillsoftMarker = new DummyCyberneticsDescriptionEvent
        {
            Description = "\u0001You gain access to the Tactics skill tree.",
            ToAdd = ["\u0001You gain access to the Cudgel skill tree."],
        };
        var socialUnknown = new DummyCyberneticsDescriptionEvent { Description = "Unknown social coprocessor behavior." };
        var techUnknown = new DummyCyberneticsDescriptionEvent
        {
            Description = "Unknown tech indexer behavior.",
            ToAdd = ["Unknown Sifrah behavior."],
        };

        CyberneticsDescriptionAssignmentTranslationPatch.TranslateSingleSkillsoftEvent(singleSkillsoftMarker);
        CyberneticsDescriptionAssignmentTranslationPatch.TranslateTreeSkillsoftEvent(treeSkillsoftMarker);
        CyberneticsDescriptionAssignmentTranslationPatch.TranslateSocialCoprocessorEvent(socialUnknown);
        CyberneticsDescriptionAssignmentTranslationPatch.TranslateTechIndexerEvent(techUnknown);

        Assert.Multiple(() =>
        {
            Assert.That(singleSkillsoftMarker.Description, Is.EqualTo("You gain the skill Tinkering."));
            Assert.That(singleSkillsoftMarker.ToAdd, Is.EqualTo(new[] { "You gain the skill Proselytize." }));
            Assert.That(treeSkillsoftMarker.Description, Is.EqualTo("You gain access to the Tactics skill tree."));
            Assert.That(treeSkillsoftMarker.ToAdd, Is.EqualTo(new[] { "You gain access to the Cudgel skill tree." }));
            Assert.That(socialUnknown.Description, Is.EqualTo("Unknown social coprocessor behavior."));
            Assert.That(techUnknown.Description, Is.EqualTo("Unknown tech indexer behavior."));
            Assert.That(techUnknown.ToAdd, Is.EqualTo(new[] { "Unknown Sifrah behavior." }));
        });
    }

    private sealed class DummyBodyPart
    {
        public string? Name { get; set; }

        public string? Description { get; set; }

        public string? Manager { get; set; }
    }

    private sealed class DummyCyberneticsDescriptionEvent
    {
        public string? Description { get; set; }

        public List<string>? ToAdd { get; set; }
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
        File.WriteAllText(
            Path.Combine(dictionariesDirectory, "cybernetics-description-assignment-test.ja.json"),
            builder.ToString(),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static string EscapeJson(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r");
    }
}
