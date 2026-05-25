using System.Reflection;
using System.Text;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class DescriptionShortDescriptionPatchTests
{
    private string tempDirectory = null!;
    private string patternFilePath = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-description-short-l2", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        patternFilePath = Path.Combine(tempDirectory, "messages.ja.json");

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(tempDirectory);
        MessagePatternTranslator.ResetForTests();
        MessagePatternTranslator.SetPatternFileForTests(patternFilePath);
        File.WriteAllText(patternFilePath, "{\"patterns\":[]}\n", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        MessagePatternTranslator.ResetForTests();
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    public void DescriptionShortDescriptionPatch_TranslatesMixedJapaneseDescriptionBlock_FromRuntimeShape_WhenPatched()
    {
        WriteDictionary(
            ("The villagers of {0}", "{0}の村人たち"),
            ("lighting a beacon fire to warn their enemies", "敵に警告するために狼煙を上げたため"),
            ("selling a map of their vaults to adventurers", "冒険者に彼らの地下墓所の地図を売ったため"),
            ("digging up the remains of their ancestors", "祖先の遺骸を掘り起こしたため"));

        const string source = "温かな笑みが、時と無数の塩の欠片にそばかすのように点じられた老いた顔に広がる。猫背の体はわずかに揺れ、棘冠を戴いた短い尾が足元を払う。肩の落ちた背から第二の腕が持ち上がり、指を組んでもうひとつの顔を形作る――口はなく、砂漠の白で塗られた目だけが古代めいて空虚だ。\n-----\nLoved by the ジョッパの村人たち.\n\nHated by 馬類 for lighting a beacon fire to warn their enemies.\nDisliked by the 盲道の徒 for lighting a beacon fire to warn their enemies.\nDisliked by the イドの住民 for selling a map of their vaults to adventurers.\nHated by the villagers of アラガシュル for digging up the remains of their ancestors.";

        const string expected = "温かな笑みが、時と無数の塩の欠片にそばかすのように点じられた老いた顔に広がる。猫背の体はわずかに揺れ、棘冠を戴いた短い尾が足元を払う。肩の落ちた背から第二の腕が持ち上がり、指を組んでもうひとつの顔を形作る――口はなく、砂漠の白で塗られた目だけが古代めいて空虚だ。\n-----\nジョッパの村人たちに愛されている。\n\n馬類に憎まれている。理由: 敵に警告するために狼煙を上げたため。\n盲道の徒に嫌われている。理由: 敵に警告するために狼煙を上げたため。\nイドの住民に嫌われている。理由: 冒険者に彼らの地下墓所の地図を売ったため。\nアラガシュルの村人たちに憎まれている。理由: 祖先の遺骸を掘り起こしたため。";

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyDescriptionShortDescriptionTarget), nameof(DummyDescriptionShortDescriptionTarget.GetShortDescription)),
                postfix: new HarmonyMethod(RequirePostfix(typeof(DescriptionShortDescriptionPatch), nameof(DescriptionShortDescriptionPatch.Postfix))));

            var target = new DummyDescriptionShortDescriptionTarget(source);
            var result = target.GetShortDescription(useShort: true, useLong: false, prefix: string.Empty);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo(expected));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(DescriptionShortDescriptionPatch),
                        "Description.FactionDisposition"),
                    Is.GreaterThan(0));
                Assert.That(
                    SinkObservation.GetHitCountForTests(
                        nameof(UITextSkinTranslationPatch),
                        nameof(DescriptionShortDescriptionPatch),
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
    public void DescriptionShortDescriptionPatch_TranslatesScopedWorldModsEntries_WhenPatched()
    {
        WriteScopedDictionary(
            ("Strength Bonus Cap: no limit\nWeapon Class: Long Blades (increased penetration on critical hit)", "Strength ボーナス上限: なし\n武器カテゴリ: 長剣（クリティカル時に貫通力上昇）"),
            ("Masterwork: This weapon scores critical hits {0} of the time instead of 5%.", "傑作: この武器のクリティカル発生率は{0}（通常は5%）。"),
            ("Offhand Attack Chance: {0}%", "オフハンド命中率: {0}%"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyDescriptionShortDescriptionTarget), nameof(DummyDescriptionShortDescriptionTarget.GetShortDescription)),
                postfix: new HarmonyMethod(RequirePostfix(typeof(DescriptionShortDescriptionPatch), nameof(DescriptionShortDescriptionPatch.Postfix))));

            var compareTarget = new DummyDescriptionShortDescriptionTarget(
                "Strength Bonus Cap: no limit\nWeapon Class: Long Blades (increased penetration on critical hit)");
            var masterworkTarget = new DummyDescriptionShortDescriptionTarget(
                "{{rules|Masterwork: This weapon scores critical hits 15% of the time instead of 5%.}}");
            var offhandTarget = new DummyDescriptionShortDescriptionTarget(
                "\n{{rules|Offhand Attack Chance: 15%}}");

            Assert.Multiple(() =>
            {
                Assert.That(
                    compareTarget.GetShortDescription(useShort: true, useLong: false, prefix: string.Empty),
                    Is.EqualTo("Strength ボーナス上限: なし\n武器カテゴリ: 長剣（クリティカル時に貫通力上昇）"));
                Assert.That(
                    masterworkTarget.GetShortDescription(useShort: true, useLong: false, prefix: string.Empty),
                    Is.EqualTo("{{rules|傑作: この武器のクリティカル発生率は15%（通常は5%）。}}"));
                Assert.That(
                    offhandTarget.GetShortDescription(useShort: true, useLong: false, prefix: string.Empty),
                    Is.EqualTo("\n{{rules|オフハンド命中率: 15%}}"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void DescriptionShortDescriptionPatch_TranslatesGiganticGeneratedWorldModDescription_WhenPatched()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyDescriptionShortDescriptionTarget), nameof(DummyDescriptionShortDescriptionTarget.GetShortDescription)),
                postfix: new HarmonyMethod(RequirePostfix(typeof(DescriptionShortDescriptionPatch), nameof(DescriptionShortDescriptionPatch.Postfix))));

            const string source = "{{rules|Gigantic: This weapon has +3 damage and cleaves for -3 AV. It can only be equipped by gigantic creatures.}}";
            var target = new DummyDescriptionShortDescriptionTarget(source);
            var result = target.GetShortDescription(useShort: true, useLong: false, prefix: string.Empty);

            Assert.Multiple(() =>
            {
                Assert.That(
                    result,
                    Is.EqualTo("{{rules|巨大: この武器はダメージ+3、装甲切断でAV-3を与える。これは巨大な生物しか装備できない。}}"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(DescriptionShortDescriptionPatch),
                        "Description.WorldMods"),
                    Is.GreaterThan(0));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void DescriptionShortDescriptionPatch_TranslatesPitMaterialRuntimeDescription_WhenPatched()
    {
        WriteDictionary((
            "Ground material splinters and opens onto a void.",
            "地面の素材が砕け、虚空へと口を開けている。"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyDescriptionShortDescriptionTarget), nameof(DummyDescriptionShortDescriptionTarget.GetShortDescription)),
                postfix: new HarmonyMethod(RequirePostfix(typeof(DescriptionShortDescriptionPatch), nameof(DescriptionShortDescriptionPatch.Postfix))));

            const string source = "Ground material splinters and opens onto a void.";
            var target = new DummyDescriptionShortDescriptionTarget(source);
            var result = target.GetShortDescription(useShort: true, useLong: false, prefix: string.Empty);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo("地面の素材が砕け、虚空へと口を開けている。"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(DescriptionShortDescriptionPatch),
                        "Description.ExactLeaf"),
                    Is.GreaterThan(0));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [TestCase("It's evil you.", "それは邪悪なあなた自身だ。")]
    [TestCase("It's negative you.", "それは負のあなた自身だ。")]
    [TestCase("It's refracted you.", "それは屈折したあなた自身だ。")]
    public void DescriptionShortDescriptionPatch_TranslatesEvilTwinRuntimeDescriptions_WhenPatched(
        string source,
        string expected)
    {
        WriteDictionary(
            ("It's evil you.", "それは邪悪なあなた自身だ。"),
            ("It's negative you.", "それは負のあなた自身だ。"),
            ("It's refracted you.", "それは屈折したあなた自身だ。"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyDescriptionShortDescriptionTarget), nameof(DummyDescriptionShortDescriptionTarget.GetShortDescription)),
                postfix: new HarmonyMethod(RequirePostfix(typeof(DescriptionShortDescriptionPatch), nameof(DescriptionShortDescriptionPatch.Postfix))));

            var target = new DummyDescriptionShortDescriptionTarget(source);
            var result = target.GetShortDescription(useShort: true, useLong: false, prefix: string.Empty);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo(expected));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(DescriptionShortDescriptionPatch),
                        "Description.ExactLeaf"),
                    Is.GreaterThan(0));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void DescriptionShortDescriptionPatch_RecordsOwnerRouteTransforms_WithoutUITextSkinSinkObservation_WhenPatched()
    {
        WriteDictionary(("Charged item", "帯電したアイテム"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyDescriptionShortDescriptionTarget), nameof(DummyDescriptionShortDescriptionTarget.GetShortDescription)),
                postfix: new HarmonyMethod(RequirePostfix(typeof(DescriptionShortDescriptionPatch), nameof(DescriptionShortDescriptionPatch.Postfix))));

            const string source = "Charged item";
            var target = new DummyDescriptionShortDescriptionTarget(source);
            var result = target.GetShortDescription(useShort: true, useLong: false, prefix: string.Empty);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo("帯電したアイテム"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(DescriptionShortDescriptionPatch),
                        "Description.ExactLeaf"),
                    Is.GreaterThan(0));
                Assert.That(
                    SinkObservation.GetHitCountForTests(
                        nameof(UITextSkinTranslationPatch),
                        nameof(DescriptionShortDescriptionPatch),
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
    public void DescriptionShortDescriptionPatch_TranslatesVillageDescriptionPattern_WhenPatched()
    {
        WriteDictionary(
            ("people", "人々"),
            ("gather", "集う"),
            ("reverence", "崇敬"));
        WriteMessagePatternDictionary((
            "^(.+?), ((?i:someone|somebody|a mysterious person|a child|a woman|a man|a baby|some group|some sect|some organization|some party|some cabal|some group of friends|some group of lovers|people|folk|communities|kindred|families|kin|kind|kinsfolk|tribe|clan)) ((?i:gather|come together|habitate together|cluster|assemble|live together)) in ((?i:reverence|awe|worship|adoration|devotion|piety|deification|love|honor)) of (.+?)\\.$",
            "{0}、{t1}が{4}に{t3}して{t2}。"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyDescriptionShortDescriptionTarget), nameof(DummyDescriptionShortDescriptionTarget.GetShortDescription)),
                postfix: new HarmonyMethod(RequirePostfix(typeof(DescriptionShortDescriptionPatch), nameof(DescriptionShortDescriptionPatch.Postfix))));

            var target = new DummyDescriptionShortDescriptionTarget(
                "red sandstone bluffs, people gather in reverence of the chrome idol.");

            Assert.Multiple(() =>
            {
                Assert.That(
                    target.GetShortDescription(useShort: true, useLong: false, prefix: string.Empty),
                    Is.EqualTo("red sandstone bluffs、人々がthe chrome idolに崇敬して集う。"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(DescriptionShortDescriptionPatch),
                        "Description.Pattern"),
                    Is.GreaterThan(0));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void DescriptionShortDescriptionPatch_TranslatesGeneratedRandomStatueLine_WhenPatched()
    {
        WriteMessagePatternDictionary((
            "^This statue worked from (.+?) intricately depicts (?:the |a |an )?(.+?):$",
            "{t0}から作られたこの像には{1}が精巧に描かれている:"));
        WriteDictionary(("stone", "石"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyDescriptionShortDescriptionTarget), nameof(DummyDescriptionShortDescriptionTarget.GetShortDescription)),
                postfix: new HarmonyMethod(RequirePostfix(typeof(DescriptionShortDescriptionPatch), nameof(DescriptionShortDescriptionPatch.Postfix))));

            var target = new DummyDescriptionShortDescriptionTarget(
                "This statue worked from stone intricately depicts a 山羊人の種播き:\n\n古い銘文が刻まれている。");

            Assert.Multiple(() =>
            {
                Assert.That(
                    target.GetShortDescription(useShort: true, useLong: false, prefix: string.Empty),
                    Is.EqualTo("石から作られたこの像には山羊人の種播きが精巧に描かれている:\n\n古い銘文が刻まれている。"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(DescriptionShortDescriptionPatch),
                        "Description.Pattern"),
                    Is.GreaterThan(0));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void DescriptionShortDescriptionPatch_TranslatesDynamicWorldModsTemplates_WhenPatched()
    {
        WriteScopedDictionary(
            ("Adds item modification: {0}", "アイテム改造: {0}"),
            ("Counterweighted: Adds {0} to hit.", "つり合い調整: 命中に{0}のボーナスを与える。"),
            ("Co-Processor: When powered, this item grants {0} {1} and provides {2} units of compute power to the local lattice.", "共同処理装置: 通電中、{1}に{0}を与え、局所格子に{2}ユニットの演算力を供給する。"),
            ("Intelligence", "知力"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyDescriptionShortDescriptionTarget), nameof(DummyDescriptionShortDescriptionTarget.GetShortDescription)),
                postfix: new HarmonyMethod(RequirePostfix(typeof(DescriptionShortDescriptionPatch), nameof(DescriptionShortDescriptionPatch.Postfix))));

            var target = new DummyDescriptionShortDescriptionTarget(
                "Co-processor: When powered, this item grants +2 Intelligence and provides 13 units of compute power to the local lattice.\nAdds item modification: Counterweighted: Adds +2 to hit.");

            Assert.Multiple(() =>
            {
                Assert.That(
                    target.GetShortDescription(useShort: true, useLong: false, prefix: string.Empty),
                    Is.EqualTo("共同処理装置: 通電中、Intelligenceに+2を与え、局所格子に13ユニットの演算力を供給する。\nアイテム改造: つり合い調整: 命中に+2のボーナスを与える。"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(DescriptionShortDescriptionPatch),
                        "Description.WorldMods"),
                    Is.GreaterThan(0));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void DescriptionShortDescriptionPatch_TranslatesFactionDispositionLines_WhenPatched()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyDescriptionShortDescriptionTarget), nameof(DummyDescriptionShortDescriptionTarget.GetShortDescription)),
                postfix: new HarmonyMethod(RequirePostfix(typeof(DescriptionShortDescriptionPatch), nameof(DescriptionShortDescriptionPatch.Postfix))));

            var lovedTarget = new DummyDescriptionShortDescriptionTarget("Loved by the Joppa villagers.");
            var hatedTarget = new DummyDescriptionShortDescriptionTarget("Hated by apes.");
            var dislikedTarget = new DummyDescriptionShortDescriptionTarget("Disliked by goatfolk.");

            Assert.Multiple(() =>
            {
                Assert.That(
                    lovedTarget.GetShortDescription(useShort: true, useLong: false, prefix: string.Empty),
                    Is.EqualTo("the Joppa villagersに愛されている。"));
                Assert.That(
                    hatedTarget.GetShortDescription(useShort: true, useLong: false, prefix: string.Empty),
                    Is.EqualTo("apesに憎まれている。"));
                Assert.That(
                    dislikedTarget.GetShortDescription(useShort: true, useLong: false, prefix: string.Empty),
                    Is.EqualTo("goatfolkに嫌われている。"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static string CreateHarmonyId()
    {
        return $"qudjp.tests.{Guid.NewGuid():N}";
    }

    private static MethodInfo RequireMethod(Type type, string methodName)
    {
        return AccessTools.Method(type, methodName, new[] { typeof(bool), typeof(bool), typeof(string) })
            ?? throw new InvalidOperationException($"Method not found: {type.FullName}.{methodName}");
    }

    private static MethodInfo RequirePostfix(Type type, string methodName)
    {
        return AccessTools.Method(type, methodName)
            ?? throw new InvalidOperationException($"Method not found: {type.FullName}.{methodName}");
    }

    private void WriteDictionary(params (string key, string text)[] entries)
    {
        WriteDictionaryToFile("description-short-l2.ja.json", entries);
    }

    private void WriteScopedDictionary(params (string key, string text)[] entries)
    {
        WriteDictionaryToFile("world-mods.ja.json", entries);
    }

    private void WriteDictionaryToFile(string fileName, (string key, string text)[] entries)
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
        File.WriteAllText(
            Path.Combine(tempDirectory, fileName),
            builder.ToString(),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private void WriteMessagePatternDictionary(params (string pattern, string template)[] patterns)
    {
        var builder = new StringBuilder();
        builder.Append("{\"patterns\":[");
        for (var index = 0; index < patterns.Length; index++)
        {
            if (index > 0)
            {
                builder.Append(',');
            }

            builder.Append("{\"pattern\":\"");
            builder.Append(EscapeJson(patterns[index].pattern));
            builder.Append("\",\"template\":\"");
            builder.Append(EscapeJson(patterns[index].template));
            builder.Append("\"}");
        }

        builder.Append("]}");
        builder.AppendLine();
        File.WriteAllText(patternFilePath, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
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
