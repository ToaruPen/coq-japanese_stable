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
