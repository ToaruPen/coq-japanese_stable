using System;
using System.IO;
using System.Reflection;
using System.Text;
using HarmonyLib;
using NUnit.Framework;
using QudJP.Patches;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class DescriptionLongDescriptionPatchTests
{
    private static readonly UTF8Encoding Utf8WithoutBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private string tempDirectory = null!;
    private string patternFilePath = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-description-long-l2", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        patternFilePath = Path.Combine(tempDirectory, "messages.ja.json");

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(tempDirectory);
        MessagePatternTranslator.ResetForTests();
        MessagePatternTranslator.SetPatternFileForTests(patternFilePath);
        File.WriteAllText(patternFilePath, "{\"patterns\":[]}\n", Utf8WithoutBom);
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
    public void Postfix_TranslatesMixedJapaneseDescriptionBlock_FromRuntimeShape_WhenPatched()
    {
        WriteDictionary(
            ("The villagers of {0}", "{0}の村人たち"),
            ("lighting a beacon fire to warn their enemies", "敵に警告するために狼煙を上げたため"),
            ("selling a map of their vaults to adventurers", "冒険者に彼らの地下墓所の地図を売ったため"),
            ("digging up the remains of their ancestors", "祖先の遺骸を掘り起こしたため"));

        const string source = "温かな笑みが、時と無数の塩の欠片にそばかすのように点じられた老いた顔に広がる。猫背の体はわずかに揺れ、棘冠を戴いた短い尾が足元を払う。肩の落ちた背から第二の腕が持ち上がり、指を組んでもうひとつの顔を形作る――口はなく、砂漠の白で塗られた目だけが古代めいて空虚だ。\n-----\nLoved by the ジョッパの村人たち.\n\nHated by 馬類 for lighting a beacon fire to warn their enemies.\nDisliked by the 盲道の徒 for lighting a beacon fire to warn their enemies.\nDisliked by the イドの住民 for selling a map of their vaults to adventurers.\nPhysical features: 毒針\nEquipped: 黒のローブ, 眼鏡, クリスティールの戦斧, ステッキ, サンダル\nHated by the villagers of アラガシュル for digging up the remains of their ancestors.";

        const string expected = "温かな笑みが、時と無数の塩の欠片にそばかすのように点じられた老いた顔に広がる。猫背の体はわずかに揺れ、棘冠を戴いた短い尾が足元を払う。肩の落ちた背から第二の腕が持ち上がり、指を組んでもうひとつの顔を形作る――口はなく、砂漠の白で塗られた目だけが古代めいて空虚だ。\n-----\nジョッパの村人たちに愛されている。\n\n馬類に憎まれている。理由: 敵に警告するために狼煙を上げたため。\n盲道の徒に嫌われている。理由: 敵に警告するために狼煙を上げたため。\nイドの住民に嫌われている。理由: 冒険者に彼らの地下墓所の地図を売ったため。\n身体的特徴: 毒針\n装備: 黒のローブ、眼鏡、クリスティールの戦斧、ステッキ、サンダル\nアラガシュルの村人たちに憎まれている。理由: 祖先の遺骸を掘り起こしたため。";

        RunWithDescriptionPatch(() =>
        {
            var target = new DummyDescriptionTarget(source);
            var builder = new StringBuilder();
            target.GetLongDescription(builder);

            Assert.Multiple(() =>
            {
                Assert.That(builder.ToString(), Is.EqualTo(expected));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(DescriptionLongDescriptionPatch),
                        "Description.FactionDisposition"),
                    Is.GreaterThan(0));
                Assert.That(
                    SinkObservation.GetHitCountForTests(
                        nameof(UITextSkinTranslationPatch),
                        nameof(DescriptionLongDescriptionPatch),
                        SinkObservation.ObservationOnlyDetail,
                        source,
                        source),
                    Is.EqualTo(0));
            });
        });
    }

    [Test]
    public void Postfix_TranslatesAppendedDescription_WhenPatched()
    {
        WriteDictionary(("It crackles with static.", "それは静電気を散らしている。"));

        RunWithDescriptionPatch(() =>
        {
            var target = new DummyDescriptionTarget("It crackles with static.");
            var builder = new StringBuilder();
            target.GetLongDescription(builder);

            Assert.That(builder.ToString(), Is.EqualTo("それは静電気を散らしている。"));
        });
    }

    [Test]
    public void Postfix_RecordsOwnerRouteTransforms_WithoutUITextSkinSinkObservation_WhenPatched()
    {
        WriteDictionary(("It crackles with static.", "それは静電気を散らしている。"));

        RunWithDescriptionPatch(() =>
        {
            const string source = "It crackles with static.";
            var target = new DummyDescriptionTarget(source);
            var builder = new StringBuilder();
            target.GetLongDescription(builder);

            Assert.Multiple(() =>
            {
                Assert.That(builder.ToString(), Is.EqualTo("それは静電気を散らしている。"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(DescriptionLongDescriptionPatch),
                        "Description.ExactLeaf"),
                    Is.GreaterThan(0));
                Assert.That(
                    SinkObservation.GetHitCountForTests(
                        nameof(UITextSkinTranslationPatch),
                        nameof(DescriptionLongDescriptionPatch),
                        SinkObservation.ObservationOnlyDetail,
                        source,
                        source),
                    Is.EqualTo(0));
            });
        });
    }

    [Test]
    public void Postfix_TranslatesScopedWorldModsExactEntry_WithoutUITextSkinSinkObservation_WhenPatched()
    {
        WriteScopedDictionary(
            ("Airfoil: This item can be thrown at +4 throwing range.", "エアフォイル: この品は投擲射程が+4される。"));

        RunWithDescriptionPatch(() =>
        {
            const string source = "Airfoil: This item can be thrown at +4 throwing range.";
            var target = new DummyDescriptionTarget(source);
            var builder = new StringBuilder();
            target.GetLongDescription(builder);

            Assert.Multiple(() =>
            {
                Assert.That(builder.ToString(), Is.EqualTo("エアフォイル: この品は投擲射程が+4される。"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(DescriptionLongDescriptionPatch),
                        "Description.WorldMods"),
                    Is.GreaterThan(0));
                Assert.That(
                    SinkObservation.GetHitCountForTests(
                        nameof(UITextSkinTranslationPatch),
                        nameof(DescriptionLongDescriptionPatch),
                        SinkObservation.ObservationOnlyDetail,
                        source,
                        source),
                    Is.EqualTo(0));
            });
        });
    }

    [Test]
    public void Postfix_PreservesExistingPrefix_WhenPatched()
    {
        WriteDictionary(("It crackles with static.", "それは静電気を散らしている。"));

        RunWithDescriptionPatch(() =>
        {
            var target = new DummyDescriptionTarget("It crackles with static.");
            var builder = new StringBuilder("prefix: ");
            target.GetLongDescription(builder);

            Assert.That(builder.ToString(), Is.EqualTo("prefix: それは静電気を散らしている。"));
        });
    }

    [Test]
    public void Postfix_PreservesColorCodes_WhenPatched()
    {
        WriteDictionary(("Charged item", "帯電したアイテム"));

        RunWithDescriptionPatch(() =>
        {
            var target = new DummyDescriptionTarget("{{C|Charged item}}");
            var builder = new StringBuilder();
            target.GetLongDescription(builder);

            Assert.That(builder.ToString(), Is.EqualTo("{{C|帯電したアイテム}}"));
        });
    }

    [Test]
    public void Postfix_PreservesColorCodes_WhenVillagersTargetReorders()
    {
        WriteDictionary(
            ("The villagers of {0}", "{0}の村人たち"),
            ("digging up the remains of their ancestors", "祖先の遺骸を掘り起こしたため"));

        RunWithDescriptionPatch(() =>
        {
            var target = new DummyDescriptionTarget("Hated by the villagers of {{C|アラガシュル}} for digging up the remains of their ancestors.");
            var builder = new StringBuilder();
            target.GetLongDescription(builder);

            Assert.Multiple(() =>
            {
                Assert.That(
                    builder.ToString(),
                    Is.EqualTo("{{C|アラガシュル}}の村人たちに憎まれている。理由: 祖先の遺骸を掘り起こしたため。"));
                Assert.That(
                    SinkObservation.GetHitCountForTests(
                        nameof(UITextSkinTranslationPatch),
                        nameof(DescriptionLongDescriptionPatch),
                        SinkObservation.ObservationOnlyDetail,
                        "Hated by the villagers of {{C|アラガシュル}} for digging up the remains of their ancestors.",
                        "Hated by the villagers of {{C|アラガシュル}} for digging up the remains of their ancestors."),
                    Is.EqualTo(0));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(DescriptionLongDescriptionPatch),
                        "Description.FactionDisposition"),
                    Is.GreaterThan(0));
            });
        });
    }

    [Test]
    public void Postfix_PreservesColorCodes_WhenLeadingArticleStripReordersColoredTarget()
    {
        WriteDictionary(("lighting a beacon fire to warn their enemies", "敵に警告するために狼煙を上げたため"));

        RunWithDescriptionPatch(() =>
        {
            var target = new DummyDescriptionTarget("Hated by the {{C|盲道の徒}} for lighting a beacon fire to warn their enemies.");
            var builder = new StringBuilder();
            target.GetLongDescription(builder);

            Assert.Multiple(() =>
            {
                Assert.That(
                    builder.ToString(),
                    Is.EqualTo("{{C|盲道の徒}}に憎まれている。理由: 敵に警告するために狼煙を上げたため。"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(DescriptionLongDescriptionPatch),
                        "Description.FactionDisposition"),
                    Is.GreaterThan(0));
                Assert.That(
                    SinkObservation.GetHitCountForTests(
                        nameof(UITextSkinTranslationPatch),
                        nameof(DescriptionLongDescriptionPatch),
                        SinkObservation.ObservationOnlyDetail,
                        "Hated by the {{C|盲道の徒}} for lighting a beacon fire to warn their enemies.",
                        "Hated by the {{C|盲道の徒}} for lighting a beacon fire to warn their enemies."),
                    Is.EqualTo(0));
            });
        });
    }

    [Test]
    public void Postfix_DoesNotReattachArticleOnlyColorWrapper_WhenLeadingArticleIsStripped()
    {
        WriteDictionary(("lighting a beacon fire to warn their enemies", "敵に警告するために狼煙を上げたため"));

        RunWithDescriptionPatch(() =>
        {
            const string source = "Hated by {{C|the}} 盲道の徒 for lighting a beacon fire to warn their enemies.";
            var target = new DummyDescriptionTarget(source);
            var builder = new StringBuilder();
            target.GetLongDescription(builder);

            Assert.Multiple(() =>
            {
                Assert.That(
                    builder.ToString(),
                    Is.EqualTo("盲道の徒に憎まれている。理由: 敵に警告するために狼煙を上げたため。"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(DescriptionLongDescriptionPatch),
                        "Description.FactionDisposition"),
                    Is.GreaterThan(0));
                Assert.That(
                    SinkObservation.GetHitCountForTests(
                        nameof(UITextSkinTranslationPatch),
                        nameof(DescriptionLongDescriptionPatch),
                        SinkObservation.ObservationOnlyDetail,
                        source,
                        source),
                    Is.EqualTo(0));
            });
        });
    }

    [Test]
    public void Postfix_PreservesOriginalVillageTarget_WhenVillageTemplateTranslationIsMissing()
    {
        WriteDictionary(("digging up the remains of their ancestors", "祖先の遺骸を掘り起こしたため"));

        RunWithDescriptionPatch(() =>
        {
            const string source = "Hated by the villagers of アラガシュル for digging up the remains of their ancestors.";
            var target = new DummyDescriptionTarget(source);
            var builder = new StringBuilder();
            target.GetLongDescription(builder);

            Assert.Multiple(() =>
            {
                Assert.That(
                    builder.ToString(),
                    Is.EqualTo("the villagers of アラガシュルに憎まれている。理由: 祖先の遺骸を掘り起こしたため。"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(DescriptionLongDescriptionPatch),
                        "Description.FactionDisposition"),
                    Is.GreaterThan(0));
                Assert.That(
                    SinkObservation.GetHitCountForTests(
                        nameof(UITextSkinTranslationPatch),
                        nameof(DescriptionLongDescriptionPatch),
                        SinkObservation.ObservationOnlyDetail,
                        source,
                        source),
                    Is.EqualTo(0));
            });
        });
    }

    [Test]
    public void Postfix_PreservesColorCodes_WhenWholeVillagersTargetIsWrapped()
    {
        WriteDictionary(
            ("The villagers of {0}", "{0}の村人たち"),
            ("digging up the remains of their ancestors", "祖先の遺骸を掘り起こしたため"));

        RunWithDescriptionPatch(() =>
        {
            var target = new DummyDescriptionTarget("Hated by {{C|the villagers of アラガシュル}} for digging up the remains of their ancestors.");
            var builder = new StringBuilder();
            target.GetLongDescription(builder);

            Assert.Multiple(() =>
            {
                Assert.That(
                    builder.ToString(),
                    Is.EqualTo("{{C|アラガシュルの村人たち}}に憎まれている。理由: 祖先の遺骸を掘り起こしたため。"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(DescriptionLongDescriptionPatch),
                        "Description.FactionDisposition"),
                    Is.GreaterThan(0));
                Assert.That(
                    SinkObservation.GetHitCountForTests(
                        nameof(UITextSkinTranslationPatch),
                        nameof(DescriptionLongDescriptionPatch),
                        SinkObservation.ObservationOnlyDetail,
                        "Hated by {{C|the villagers of アラガシュル}} for digging up the remains of their ancestors.",
                        "Hated by {{C|the villagers of アラガシュル}} for digging up the remains of their ancestors."),
                    Is.EqualTo(0));
            });
        });
    }

    [Test]
    public void Postfix_PreservesNestedWrappers_WhenVillagersTargetReorders()
    {
        WriteDictionary(
            ("The villagers of {0}", "{0}の村人たち"),
            ("digging up the remains of their ancestors", "祖先の遺骸を掘り起こしたため"));

        RunWithDescriptionPatch(() =>
        {
            const string source = "Hated by {{W|the villagers of {{C|アラガシュル}}}} for digging up the remains of their ancestors.";
            var target = new DummyDescriptionTarget(source);
            var builder = new StringBuilder();
            target.GetLongDescription(builder);

            Assert.Multiple(() =>
            {
                Assert.That(
                    builder.ToString(),
                    Is.EqualTo("{{W|{{C|アラガシュル}}の村人たち}}に憎まれている。理由: 祖先の遺骸を掘り起こしたため。"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(DescriptionLongDescriptionPatch),
                        "Description.FactionDisposition"),
                    Is.GreaterThan(0));
                Assert.That(
                    SinkObservation.GetHitCountForTests(
                        nameof(UITextSkinTranslationPatch),
                        nameof(DescriptionLongDescriptionPatch),
                        SinkObservation.ObservationOnlyDetail,
                        source,
                        source),
                    Is.EqualTo(0));
            });
        });
    }

    [Test]
    public void Postfix_PreservesWholeLineWrapper_WhenVillagersTargetReorders()
    {
        WriteDictionary(
            ("The villagers of {0}", "{0}の村人たち"),
            ("digging up the remains of their ancestors", "祖先の遺骸を掘り起こしたため"));

        RunWithDescriptionPatch(() =>
        {
            const string source = "{{W|Hated by the villagers of アラガシュル for digging up the remains of their ancestors.}}";
            var target = new DummyDescriptionTarget(source);
            var builder = new StringBuilder();
            target.GetLongDescription(builder);

            Assert.Multiple(() =>
            {
                Assert.That(
                    builder.ToString(),
                    Is.EqualTo("{{W|アラガシュルの村人たちに憎まれている。理由: 祖先の遺骸を掘り起こしたため。}}"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(DescriptionLongDescriptionPatch),
                        "Description.FactionDisposition"),
                    Is.GreaterThan(0));
                Assert.That(
                    SinkObservation.GetHitCountForTests(
                        nameof(UITextSkinTranslationPatch),
                        nameof(DescriptionLongDescriptionPatch),
                        SinkObservation.ObservationOnlyDetail,
                        source,
                        source),
                    Is.EqualTo(0));
            });
        });
    }

    [Test]
    public void Postfix_PassesThroughUnknownDescription_WhenPatched()
    {
        WriteDictionary(("Known text", "既知の文"));

        RunWithDescriptionPatch(() =>
        {
            var target = new DummyDescriptionTarget("Unknown long description");
            var builder = new StringBuilder();
            target.GetLongDescription(builder);

            Assert.That(builder.ToString(), Is.EqualTo("Unknown long description"));
        });
    }

    [Test]
    public void Postfix_ObservationOnly_LeavesStatAbbreviationsUnchanged_WhenPatched()
    {
        WriteDictionary(("STR", "筋力"), ("+1 STR", "+1 筋力"));

        RunWithDescriptionPatch(() =>
        {
            var abbreviationTarget = new DummyDescriptionTarget("STR");
            var abbreviationBuilder = new StringBuilder();
            abbreviationTarget.GetLongDescription(abbreviationBuilder);

            var signedTarget = new DummyDescriptionTarget("+1 STR");
            var signedBuilder = new StringBuilder();
            signedTarget.GetLongDescription(signedBuilder);

            Assert.Multiple(() =>
            {
                Assert.That(abbreviationBuilder.ToString(), Is.EqualTo("STR"));
                Assert.That(signedBuilder.ToString(), Is.EqualTo("+1 STR"));
            });
        });
    }

    [Test]
    public void Postfix_TranslatesCompareStatusLines_WhenPatched()
    {
        WriteDictionary(
            ("Strength", "筋力"),
            ("Bonus Cap:", "ボーナス上限:"),
            ("Weapon Class:", "武器カテゴリ:"),
            ("Long Blades (increased penetration on critical hit)", "長剣（クリティカル時に貫通力上昇）"),
            ("no limit", "なし"));

        RunWithDescriptionPatch(() =>
        {
            var capTarget = new DummyDescriptionTarget("Strength Bonus Cap: no limit");
            var capBuilder = new StringBuilder();
            capTarget.GetLongDescription(capBuilder);

            var egoCapTarget = new DummyDescriptionTarget("Ego Bonus Cap: 2");
            var egoCapBuilder = new StringBuilder();
            egoCapTarget.GetLongDescription(egoCapBuilder);

            var weaponClassTarget =
                new DummyDescriptionTarget("Weapon Class: Long Blades (increased penetration on critical hit)");
            var weaponClassBuilder = new StringBuilder();
            weaponClassTarget.GetLongDescription(weaponClassBuilder);

            Assert.Multiple(() =>
            {
                Assert.That(capBuilder.ToString(), Is.EqualTo("筋力ボーナス上限: なし"));
                Assert.That(egoCapBuilder.ToString(), Is.EqualTo("Ego ボーナス上限: 2"));
                Assert.That(weaponClassBuilder.ToString(), Is.EqualTo("武器カテゴリ: 長剣（クリティカル時に貫通力上昇）"));
            });
        });
    }

    [Test]
    public void Postfix_TranslatesVillageDescriptionPattern_WhenPatched()
    {
        WriteDictionary(
            ("conclave", "会合"),
            ("kin", "血縁"),
            ("some organization", "ある組織"));
        WriteMessagePatternDictionary((
            "^(.+?), there's a ((?i:gathering|conclave|congregation|settlement|band|flock|society)) of (.+?) and their ((?i:folk|communities|kindred|families|kin|kind|kinsfolk|tribe|clan))\\.$",
            "{0}、{t2}とその{t3}の{t1}がある。"));

        RunWithDescriptionPatch(() =>
        {
            var target = new DummyDescriptionTarget("sun-baked ruins, there's a conclave of some organization and their kin.");
            var builder = new StringBuilder();
            target.GetLongDescription(builder);

            Assert.Multiple(() =>
            {
                Assert.That(builder.ToString(), Is.EqualTo("sun-baked ruins、ある組織とその血縁の会合がある。"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(DescriptionLongDescriptionPatch),
                        "Description.Pattern"),
                    Is.GreaterThan(0));
            });
        });
    }

    [Test]
    public void Postfix_TranslatesPhysicalFeaturesAndEquippedLines_WhenPatched()
    {
        WriteDictionary(
            ("stinger", "毒針"),
            ("black robe", "黒のローブ"));

        RunWithDescriptionPatch(() =>
        {
            var target = new DummyDescriptionTarget("Physical features: stinger\nEquipped: black robe");
            var builder = new StringBuilder();
            target.GetLongDescription(builder);

            Assert.That(builder.ToString(), Is.EqualTo("身体的特徴: 毒針\n装備: 黒のローブ"));
        });
    }

    [Test]
    public void Postfix_TranslatesReasonBearingDispositionLine_WithColoredFactionTarget_WhenPatched()
    {
        WriteDictionary(("giving alms to pilgrims", "巡礼者に施しをしたため"));

        RunWithDescriptionPatch(() =>
        {
            var target = new DummyDescriptionTarget("Admired by {{C|the Mechanimists}} for giving alms to pilgrims.");
            var builder = new StringBuilder();
            target.GetLongDescription(builder);

            Assert.That(builder.ToString(), Is.EqualTo("{{C|the Mechanimists}}に敬愛されている。理由: 巡礼者に施しをしたため。"));
        });
    }

    private static string CreateHarmonyId()
    {
        return $"qudjp.tests.{Guid.NewGuid():N}";
    }

    private static MethodInfo RequireMethod(Type type, string methodName)
    {
        return AccessTools.Method(type, methodName)
            ?? throw new InvalidOperationException($"Method not found: {type.FullName}.{methodName}");
    }

    private void WriteDictionary(params (string key, string text)[] entries)
    {
        var builder = new StringBuilder();
        builder.Append("{\"entries\":[");
        AppendEntries(builder, entries);
        builder.AppendLine("]}");
        WriteDictionaryFile(builder.ToString());
    }

    private void WriteScopedDictionary(params (string key, string text)[] entries)
    {
        var builder = new StringBuilder();
        builder.Append("{\"entries\":[");
        AppendEntries(builder, entries);
        builder.AppendLine("]}");
        WriteDictionaryFile(builder.ToString(), "world-mods.ja.json");
    }

    private static string EscapeJson(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }

    private static void AppendEntries(StringBuilder builder, IReadOnlyList<(string key, string text)> entries)
    {
        for (var index = 0; index < entries.Count; index++)
        {
            if (index > 0)
            {
                builder.Append(',');
            }

            var (key, text) = entries[index];
            builder.Append("{\"key\":\"");
            builder.Append(EscapeJson(key));
            builder.Append("\",\"text\":\"");
            builder.Append(EscapeJson(text));
            builder.Append("\"}");
        }
    }

    private static MethodInfo DescriptionMethod => RequireMethod(typeof(DummyDescriptionTarget), nameof(DummyDescriptionTarget.GetLongDescription));

    private static HarmonyMethod DescriptionPrefix =>
        new HarmonyMethod(RequireMethod(typeof(DescriptionLongDescriptionPatch), nameof(DescriptionLongDescriptionPatch.Prefix)));

    private static HarmonyMethod DescriptionPostfix =>
        new HarmonyMethod(RequireMethod(typeof(DescriptionLongDescriptionPatch), nameof(DescriptionLongDescriptionPatch.Postfix)));

    private static void RunWithDescriptionPatch(Action assertion)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: DescriptionMethod,
                prefix: DescriptionPrefix,
                postfix: DescriptionPostfix);
            assertion();
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private void WriteDictionaryFile(string content)
    {
        var path = Path.Combine(tempDirectory, "description-long-l2.ja.json");
        File.WriteAllText(path, content, Utf8WithoutBom);
    }

    private void WriteDictionaryFile(string content, string fileName)
    {
        var path = Path.Combine(tempDirectory, fileName);
        File.WriteAllText(path, content, Utf8WithoutBom);
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
        File.WriteAllText(patternFilePath, builder.ToString(), Utf8WithoutBom);
    }

    private sealed class DummyDescriptionTarget
    {
        private readonly string appendedText;

        public DummyDescriptionTarget(string appendedText)
        {
            this.appendedText = appendedText;
        }

        public void GetLongDescription(StringBuilder SB)
        {
            SB.Append(appendedText);
        }
    }
}
