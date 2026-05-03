using System.Reflection;
using System.Text;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
[NonParallelizable]
public sealed class PlayerStatusBarProducerTranslationHelpersTests
{
    private static readonly UTF8Encoding Utf8WithoutBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private string tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-player-status-bar-helper-l1", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(tempDirectory);
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    public void TranslateStringDataValue_TranslatesFoodWaterSequence()
    {
        WriteDictionary(("Sated", "満腹"), ("Quenched", "潤沢"));

        var translated = InvokeHelperStringMethod(
            "TranslateStringDataValue",
            "FoodWater",
            "Sated Quenched",
            "PlayerStatusBarProducerTranslationPatch.BeginEndTurn");

        Assert.That(translated, Is.EqualTo("満腹 潤沢"));
    }

    [Test]
    public void TranslateStringDataValue_TranslatesZoneDisplayName()
    {
        WriteDictionary(("World Map", "ワールドマップ"));

        var translated = InvokeHelperStringMethod(
            "TranslateStringDataValue",
            "Zone",
            "World Map",
            "PlayerStatusBarProducerTranslationPatch.BeginEndTurn");

        Assert.That(translated, Is.EqualTo("ワールドマップ"));
    }

    [Test]
    public void TranslateStringDataValue_TranslatesCompositeZoneDisplayName()
    {
        WriteDictionary(
            ("Salt Marsh", "塩の湿地"),
            ("Joppa", "ジョッパ"),
            ("{0} strata deep", "地下{0}層"));

        var translated = InvokeHelperStringMethod(
            "TranslateStringDataValue",
            "Zone",
            "Salt Marsh, Joppa, 42 strata deep",
            "PlayerStatusBarProducerTranslationPatch.Zone");

        Assert.That(translated, Is.EqualTo("塩の湿地, ジョッパ, 地下42層"));
    }

    [TestCase("Zone")]
    [TestCase("ZoneOnly")]
    public void TranslateStringDataValue_ZoneRoutes_FallbackToEnglish_WhenUntranslated(string fieldName)
    {
        var translated = InvokeHelperStringMethod(
            "TranslateStringDataValue",
            fieldName,
            "Salt Marsh, Unknown Place",
            "PlayerStatusBarProducerTranslationPatch.Zone");

        Assert.That(translated, Is.EqualTo("Salt Marsh, Unknown Place"));
    }

    [TestCase("Zone")]
    [TestCase("ZoneOnly")]
    public void TranslateStringDataValue_ZoneRoutes_StripDirectMarkerInput(string fieldName)
    {
        WriteDictionary(("Salt Marsh", "塩の湿地"));

        var translated = InvokeHelperStringMethod(
            "TranslateStringDataValue",
            fieldName,
            "\u0001Salt Marsh, Joppa",
            "PlayerStatusBarProducerTranslationPatch.Zone");

        Assert.That(translated, Is.EqualTo("Salt Marsh, Joppa"));
    }

    [TestCase("Zone")]
    [TestCase("ZoneOnly")]
    public void TranslateStringDataValue_ZoneRoutes_PreserveColorTagsInCompositeNames(string fieldName)
    {
        WriteDictionary(
            ("Salt Marsh", "塩の湿地"),
            ("Joppa", "ジョッパ"));

        var translated = InvokeHelperStringMethod(
            "TranslateStringDataValue",
            fieldName,
            "{{Y|Salt Marsh}}, {{C|Joppa}}",
            "PlayerStatusBarProducerTranslationPatch.Zone");

        Assert.That(translated, Is.EqualTo("{{Y|塩の湿地}}, {{C|ジョッパ}}"));
    }

    [TestCase("Zone")]
    [TestCase("ZoneOnly")]
    public void TranslateStringDataValue_ZoneRoutes_ReturnEmptyInput(string fieldName)
    {
        var translated = InvokeHelperStringMethod(
            "TranslateStringDataValue",
            fieldName,
            string.Empty,
            "PlayerStatusBarProducerTranslationPatch.Zone");

        Assert.That(translated, Is.EqualTo(string.Empty));
    }

    [Test]
    public void TranslateStringDataValue_TranslatesCalendarStatus()
    {
        WriteDictionary(
            ("Harvest Dawn", "ハーヴェスト・ドーン"),
            ("Kisu Ux", "キス・ウクス"));

        var translated = InvokeHelperStringMethod(
            "TranslateStringDataValue",
            "Time",
            "Harvest Dawn 16th of Kisu Ux",
            "PlayerStatusBarProducerTranslationPatch.BeginEndTurn");

        Assert.That(translated, Is.EqualTo("ハーヴェスト・ドーン キス・ウクス16日"));
    }

    [Test]
    public void TranslateStringDataValue_TranslatesCalendarIdesToFifteenthDay()
    {
        WriteDictionary(
            ("Beetle Moon Zenith", "ビートルムーン・ゼニス"),
            ("Nivvun Ut", "ニヴン・ウト"));

        var translated = InvokeHelperStringMethod(
            "TranslateStringDataValue",
            "Time",
            "Beetle Moon Zenith Ides of Nivvun Ut",
            "PlayerStatusBarProducerTranslationPatch.BeginEndTurn");

        Assert.That(translated, Is.EqualTo("ビートルムーン・ゼニス ニヴン・ウト15日"));
    }

    [Test]
    public void TranslateStringDataValue_PreservesColorMarkupForHpStatus()
    {
        WriteDictionary(("Seriously Wounded", "重傷"));

        var translated = InvokeHelperStringMethod(
            "TranslateStringDataValue",
            "HPBar",
            "{{Y|HP: {{R|Seriously Wounded}}}}",
            "PlayerStatusBarProducerTranslationPatch.BeginEndTurn");

        Assert.That(translated, Is.EqualTo("{{Y|HP: {{R|重傷}}}}"));
    }

    [Test]
    public void TranslateStringDataValue_PreservesTempReadoutWithoutMissingKeyNoise()
    {
        var translated = InvokeHelperStringMethod(
            "TranslateStringDataValue",
            "Temp",
            "T:25ø",
            "PlayerStatusBarProducerTranslationPatch.Temp");

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.EqualTo("T:25ø"));
            Assert.That(Translator.GetMissingKeyHitCountForTests("T:25ø"), Is.EqualTo(0));
            Assert.That(DynamicTextObservability.GetRouteFamilyHitCountForTests(
                "PlayerStatusBarProducerTranslationPatch.Temp",
                "PlayerStatusBar.Temp.PreservedReadout"), Is.EqualTo(1));
            Assert.That(Translator.GetMissingRouteHitCountForTests("<no-context>"), Is.EqualTo(0));
        });
    }

    [Test]
    public void TranslateStringDataValue_PreservesWeightReadoutWithoutMissingKeyNoise()
    {
        var translated = InvokeHelperStringMethod(
            "TranslateStringDataValue",
            "Weight",
            "62/270# {{blue|32$}}",
            "PlayerStatusBarProducerTranslationPatch.Weight");

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.EqualTo("62/270# {{blue|32$}}"));
            Assert.That(Translator.GetMissingKeyHitCountForTests("62/270# {{blue|32$}}"), Is.EqualTo(0));
            Assert.That(DynamicTextObservability.GetRouteFamilyHitCountForTests(
                "PlayerStatusBarProducerTranslationPatch.Weight",
                "PlayerStatusBar.Weight.PreservedReadout"), Is.EqualTo(1));
            Assert.That(Translator.GetMissingRouteHitCountForTests("<no-context>"), Is.EqualTo(0));
        });
    }

    [Test]
    public void TranslateXpBarText_TranslatesLevelExpLine()
    {
        WriteDictionary(("LVL", "Lv"));

        var translated = InvokeHelperStringMethod(
            "TranslateXpBarText",
            "LVL: 1 Exp: 0 / 220",
            "PlayerStatusBarProducerTranslationPatch.Update");

        Assert.That(translated, Is.EqualTo("Lv: 1 Exp: 0 / 220"));
    }

    private static string InvokeHelperStringMethod(string methodName, params object[] args)
    {
        var helperType = typeof(Translator).Assembly.GetType("QudJP.Patches.PlayerStatusBarProducerTranslationHelpers", throwOnError: false);
        Assert.That(helperType, Is.Not.Null, "PlayerStatusBarProducerTranslationHelpers type not found.");

        var method = helperType!.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
        Assert.That(method, Is.Not.Null, $"Method not found: {helperType.FullName}.{methodName}");

        var result = method!.Invoke(null, args);
        Assert.That(result, Is.TypeOf<string>());
        return (string)result!;
    }

    private void WriteDictionary(params (string key, string text)[] entries)
    {
        var path = Path.Combine(tempDirectory, "player-status-bar.ja.json");
        using var writer = new StreamWriter(path, append: false, Utf8WithoutBom);
        writer.Write("{\"entries\":[");

        for (var index = 0; index < entries.Length; index++)
        {
            if (index > 0)
            {
                writer.Write(',');
            }

            writer.Write("{\"key\":\"");
            writer.Write(EscapeJson(entries[index].key));
            writer.Write("\",\"text\":\"");
            writer.Write(EscapeJson(entries[index].text));
            writer.Write("\"}");
        }

        writer.WriteLine("]}");
    }

    private static string EscapeJson(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }
}
