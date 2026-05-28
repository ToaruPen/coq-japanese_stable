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
public sealed class LookTooltipContentPatchTests
{
    private static readonly UTF8Encoding Utf8WithoutBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private string tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-look-tooltip-l2", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(tempDirectory);
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        LocalizationAssetResolver.SetLocalizationRootForTests(null);
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    public void Postfix_TranslatesKnownTooltip_WhenPatched()
    {
        WriteDictionary(("This relic hums softly.", "この遺物はかすかに唸っている。"));

        RunWithTooltipPatch(() =>
        {
            const string source = "This relic hums softly.";
            var result = DummyLookTooltipTarget.GenerateTooltipContent(source);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo("この遺物はかすかに唸っている。"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(LookTooltipContentPatch),
                        "Description.ExactLeaf"),
                    Is.GreaterThan(0));
                Assert.That(
                    SinkObservation.GetHitCountForTests(
                        nameof(UITextSkinTranslationPatch),
                        nameof(LookTooltipContentPatch),
                        SinkObservation.ObservationOnlyDetail,
                        source,
                        source),
                    Is.EqualTo(0));
            });
        });
    }

    [Test]
    public void TranslateTooltipContent_UsesPopupFixedLeafDictionary_ForPlayerLookPopup()
    {
        var localizationRoot = GetLocalizationRoot();
        LocalizationAssetResolver.SetLocalizationRootForTests(localizationRoot);
        Translator.SetDictionaryDirectoryForTests(Path.Combine(localizationRoot, "Dictionaries"));

        const string source = "It's you.";
        var result = LookTooltipContentPatch.TranslateTooltipContent(source);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo("あなた自身だ。"));
            Assert.That(
                DynamicTextObservability.GetRouteFamilyHitCountForTests(
                    nameof(LookTooltipContentPatch),
                    "Description.ExactLeaf"),
                Is.GreaterThan(0));
        });
    }

    [Test]
    public void TranslateTooltipContent_TranslatesRuntimeMasterworkDescription_FromWorldModsDictionary()
    {
        var localizationRoot = GetLocalizationRoot();
        LocalizationAssetResolver.SetLocalizationRootForTests(localizationRoot);
        Translator.SetDictionaryDirectoryForTests(Path.Combine(localizationRoot, "Dictionaries"));

        const string source = "{{rules|Masterwork: This weapon scores critical hits 15% of the time instead of 5%.}}";
        var result = LookTooltipContentPatch.TranslateTooltipContent(source);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo("{{rules|傑作: この武器のクリティカル発生率は15%（通常は5%）。}}"));
            Assert.That(
                DynamicTextObservability.GetRouteFamilyHitCountForTests(
                    nameof(LookTooltipContentPatch),
                    "Description.WorldMods"),
                Is.GreaterThan(0));
            Assert.That(
                SinkObservation.GetHitCountForTests(
                    nameof(UITextSkinTranslationPatch),
                    nameof(LookTooltipContentPatch),
                    SinkObservation.ObservationOnlyDetail,
                    source,
                    source),
                Is.EqualTo(0));
        });
    }

    [Test]
    public void TranslateTooltipContent_TranslatesSiblingRuntimeModificationDescriptions_FromWorldModsDictionary()
    {
        var localizationRoot = GetLocalizationRoot();
        LocalizationAssetResolver.SetLocalizationRootForTests(localizationRoot);
        Translator.SetDictionaryDirectoryForTests(Path.Combine(localizationRoot, "Dictionaries"));

        Assert.Multiple(() =>
        {
            Assert.That(
                LookTooltipContentPatch.TranslateTooltipContent("{{rules|Counterweighted: Adds +2 to hit.}}"),
                Is.EqualTo("{{rules|つり合い調整: 命中に+2のボーナスを与える。}}"));
            Assert.That(
                LookTooltipContentPatch.TranslateTooltipContent("{{rules|Electrified: When powered, this weapon deals an additional 2-3 electrical damage on hit.}}"),
                Is.EqualTo("{{rules|帯電: 通電中、この武器は命中時に追加で2-3の電撃ダメージを与える。}}"));
            Assert.That(
                LookTooltipContentPatch.TranslateTooltipContent("{{rules|Scaled: This item grants the wearer +250 reputation with unshelled reptiles.}}"),
                Is.EqualTo("{{rules|鱗状の: 装着者に甲無し爬虫類との評判+250を与える。}}"));
            Assert.That(
                LookTooltipContentPatch.TranslateTooltipContent("{{rules|Fitted with beamsplitter: This weapon has a 3-way spread with each shot at -1 penetration roll.}}"),
                Is.EqualTo("{{rules|ビームスプリッタ装着: この武器は1射撃ごとに3方向へ拡散し、各射撃の貫通判定が-1される。}}"));
            Assert.That(
                LookTooltipContentPatch.TranslateTooltipContent("\n{{rules|Offhand Attack Chance: 15%}}"),
                Is.EqualTo("\n{{rules|オフハンド命中率: 15%}}"));
        });
    }

    [Test]
    public void TranslateTooltipContent_TranslatesMissileWeaponRuntimeMultipleShotLines()
    {
        const string source = "Multiple ammo used per shot: 4\nMultiple projectiles per shot: 4";

        var result = LookTooltipContentPatch.TranslateTooltipContent(source);

        Assert.That(
            result,
            Is.EqualTo("1射撃あたりの消費弾薬数: 4\n1射撃あたりの発射体数: 4"));
    }

    [Test]
    public void TranslateTooltipDisplayName_TranslatesRuntimeMasterworkScopedHeader()
    {
        var localizationRoot = GetLocalizationRoot();
        LocalizationAssetResolver.SetLocalizationRootForTests(localizationRoot);
        Translator.SetDictionaryDirectoryForTests(Path.Combine(localizationRoot, "Dictionaries"));

        var result = LookTooltipInformationWrapPatch.TranslateTooltipDisplayName("masterwork scoped チェーンピストル");

        Assert.That(result, Is.EqualTo("傑作 スコープ付き チェーンピストル"));
    }

    [Test]
    public void TranslateTooltipDisplayName_TranslatesRuntimeMasterworkScopedHeaderWithWeaponStats()
    {
        var localizationRoot = GetLocalizationRoot();
        LocalizationAssetResolver.SetLocalizationRootForTests(localizationRoot);
        Translator.SetDictionaryDirectoryForTests(Path.Combine(localizationRoot, "Dictionaries"));

        var result = LookTooltipInformationWrapPatch.TranslateTooltipDisplayName(
            "masterwork scoped チェーンピストル \u001a8 \u00031d6 [空] <AD14>");

        Assert.That(
            result,
            Is.EqualTo("傑作 スコープ付き チェーンピストル {{c|\u001a}}8 {{r|\u0003}}1d6 {{y|[空]}} {{y|<{{B|A}}{{B|D}}{{g|1}}{{g|4}}>}}"));
    }

    [Test]
    public void TranslateTooltipDisplayName_TranslatesLeadingWhitespaceRuntimeMasterworkScopedHeaderWithWeaponStats()
    {
        var localizationRoot = GetLocalizationRoot();
        LocalizationAssetResolver.SetLocalizationRootForTests(localizationRoot);
        Translator.SetDictionaryDirectoryForTests(Path.Combine(localizationRoot, "Dictionaries"));

        var result = LookTooltipInformationWrapPatch.TranslateTooltipDisplayName(
            " masterwork scoped チェーンピストル \u001a8 \u00031d6 [empty]");

        Assert.That(
            result,
            Is.EqualTo(" 傑作 スコープ付き チェーンピストル {{c|\u001a}}8 {{r|\u0003}}1d6 [空]"));
    }

    [Test]
    public void TranslateTooltipDisplayName_TranslatesThreeOrMoreRuntimeWeaponModifiersWithStats()
    {
        var localizationRoot = GetLocalizationRoot();
        LocalizationAssetResolver.SetLocalizationRootForTests(localizationRoot);
        Translator.SetDictionaryDirectoryForTests(Path.Combine(localizationRoot, "Dictionaries"));

        var result = LookTooltipInformationWrapPatch.TranslateTooltipDisplayName(
            "masterwork scoped electrified チェーンピストル \u001a8 \u00031d6 [空] <AD14>");

        Assert.That(
            result,
            Is.EqualTo("傑作 スコープ付き {{electrical|帯電}} チェーンピストル {{c|\u001a}}8 {{r|\u0003}}1d6 {{y|[空]}} {{y|<{{B|A}}{{B|D}}{{g|1}}{{g|4}}>}}"));
    }

    [Test]
    public void TranslateTooltipDisplayName_TranslatesRuntimeWeaponWithClauseWithStats()
    {
        var localizationRoot = GetLocalizationRoot();
        LocalizationAssetResolver.SetLocalizationRootForTests(localizationRoot);
        Translator.SetDictionaryDirectoryForTests(Path.Combine(localizationRoot, "Dictionaries"));

        var result = LookTooltipInformationWrapPatch.TranslateTooltipDisplayName(
            "{{C|レーザー}}ライフル with {{R-R-r-r-g-g-G-G-B-B-b-b sequence|beamsplitter}} \u001a8 \u00031d12 [broken]");

        Assert.That(
            result,
            Is.EqualTo("{{C|レーザー}}ライフル（{{R-R-r-r-g-g-G-G-B-B-b-b sequence|ビームスプリッタ装着}}） {{c|\u001a}}8 {{r|\u0003}}1d12 [{{r|破損}}]"));
    }

    [Test]
    public void TranslateTooltipLongDescription_TranslatesMakersMarkDescriptionBeforeWrapping()
    {
        const string source = "{{C|: This weapon bears the mark of スパラフチーレ.}}";

        var result = LookTooltipInformationWrapPatch.TranslateTooltipLongDescription(source);

        Assert.That(result, Is.EqualTo("{{C|スパラフチーレの印を帯びている。}}"));
    }

    [Test]
    public void TranslateTooltipLongDescription_TranslatesMakersMarkDescriptionWithVisibleMarkPrefix()
    {
        const string source = "{{R|A}}{{C|: This weapon bears the mark of スパラフチーレ.}}";

        var result = LookTooltipInformationWrapPatch.TranslateTooltipLongDescription(source);

        Assert.That(result, Is.EqualTo("{{R|A}}{{C|: スパラフチーレの印を帯びている。}}"));
    }

    [Test]
    public void TranslateTooltipLongDescription_TranslatesMakersMarkDescriptionWithTextPrefix()
    {
        const string source = "{{C|Inspection: This weapon bears the mark of スパラフチーレ.}}";

        var result = LookTooltipInformationWrapPatch.TranslateTooltipLongDescription(source);

        Assert.That(result, Is.EqualTo("{{C|Inspection: スパラフチーレの印を帯びている。}}"));
    }

    [Test]
    public void TranslateTooltipLongDescription_StripsDirectMarkerBeforeJapaneseWrap()
    {
        const string unmarked = "これは日本語の長い説明文で、折り返し処理を通ることを確認するための文章です。";
        var source = MessageFrameTranslator.MarkDirectTranslation(unmarked);

        var result = LookTooltipInformationWrapPatch.TranslateTooltipLongDescription(source);

        Assert.Multiple(() =>
        {
            Assert.That(result.IndexOf(MessageFrameTranslator.DirectTranslationMarker, StringComparison.Ordinal), Is.EqualTo(-1));
            Assert.That(result, Does.Contain("\n"));
            Assert.That(result.Replace("\n", string.Empty), Is.EqualTo(unmarked));
        });
    }

    [Test]
    public void Postfix_PreservesColorCodes_WhenPatched()
    {
        WriteDictionary(("Ancient ruin", "古代の廃墟"));

        RunWithTooltipPatch(() =>
        {
            var result = DummyLookTooltipTarget.GenerateTooltipContent("{{Y|Ancient ruin}}");

            Assert.That(result, Is.EqualTo("{{Y|古代の廃墟}}"));
        });
    }

    [Test]
    public void Postfix_PassesThroughUnknownTooltip_WhenPatched()
    {
        WriteDictionary(("Known text", "既知の文"));

        RunWithTooltipPatch(() =>
        {
            var result = DummyLookTooltipTarget.GenerateTooltipContent("Unknown tooltip text");

            Assert.That(result, Is.EqualTo("Unknown tooltip text"));
        });
    }

    [Test]
    public void Postfix_LeavesStatAbbreviationsUnchanged_WhenPatched()
    {
        WriteDictionary(("STR", "筋力"), ("+1 STR", "+1 筋力"));

        RunWithTooltipPatch(() =>
        {
            var abbreviation = DummyLookTooltipTarget.GenerateTooltipContent("STR");
            var signed = DummyLookTooltipTarget.GenerateTooltipContent("+1 STR");

            Assert.Multiple(() =>
            {
                Assert.That(abbreviation, Is.EqualTo("STR"));
                Assert.That(signed, Is.EqualTo("+1 STR"));
            });
        });
    }

    [Test]
    public void Postfix_TranslatesCompareStatusLines_WhenPatched()
    {
        WriteDictionary(
            ("Strength", "筋力"),
            ("Ego", "自我"),
            ("Bonus Cap:", "ボーナス上限:"),
            ("Weapon Class:", "武器カテゴリ:"),
            ("Long Blades (increased penetration on critical hit)", "長剣（クリティカル時に貫通力上昇）"),
            ("no limit", "なし"));

        RunWithTooltipPatch(() =>
        {
            var cap = DummyLookTooltipTarget.GenerateTooltipContent("Strength Bonus Cap: no limit");
            var egoCap = DummyLookTooltipTarget.GenerateTooltipContent("Ego Bonus Cap: 2");
            var weaponClass = DummyLookTooltipTarget.GenerateTooltipContent(
                "Weapon Class: Long Blades (increased penetration on critical hit)");

            Assert.Multiple(() =>
            {
                Assert.That(cap, Is.EqualTo("筋力ボーナス上限: なし"));
                Assert.That(egoCap, Is.EqualTo("自我ボーナス上限: 2"));
                Assert.That(weaponClass, Is.EqualTo("武器カテゴリ: 長剣（クリティカル時に貫通力上昇）"));
            });
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

    private static string GetLocalizationRoot()
    {
        return Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "../../../../../Localization"));
    }

    private void WriteDictionary(params (string key, string text)[] entries)
    {
        var builder = new StringBuilder();
        builder.Append("{\"entries\":[");
        AppendEntries(builder, entries);
        builder.AppendLine("]}");
        WriteDictionaryFile(builder.ToString());
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

    private static HarmonyMethod TooltipPostfix =>
        new HarmonyMethod(RequireMethod(typeof(LookTooltipContentPatch), nameof(LookTooltipContentPatch.Postfix)));

    private static void RunWithTooltipPatch(Action assertion)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyLookTooltipTarget), nameof(DummyLookTooltipTarget.GenerateTooltipContent)),
                postfix: TooltipPostfix);
            assertion();
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private void WriteDictionaryFile(string content)
    {
        var path = Path.Combine(tempDirectory, "look-tooltip-l2.ja.json");
        File.WriteAllText(path, content, Utf8WithoutBom);
    }

    private static class DummyLookTooltipTarget
    {
        public static string GenerateTooltipContent(string content)
        {
            return content;
        }
    }
}
