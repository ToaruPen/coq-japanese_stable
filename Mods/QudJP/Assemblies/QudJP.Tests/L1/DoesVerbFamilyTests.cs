using System.Text;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
[NonParallelizable]
public sealed class DoesVerbFamilyTests
{
    private static readonly UTF8Encoding Utf8WithoutBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private string tempDirectory = null!;
    private string patternFilePath = null!;
    private string dictionaryPath = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-does-l1", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        patternFilePath = Path.Combine(tempDirectory, "messages.ja.json");
        dictionaryPath = Path.Combine(tempDirectory, "ui-test.ja.json");

        File.Copy(
            Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "..",
                "..",
                "..",
                "..",
                "..",
                "Localization",
                "Dictionaries",
                "messages.ja.json"),
            patternFilePath);

        WriteExactDictionary(
            ("You", "あなた"),
            ("bear", "熊"),
            ("snapjaw", "スナップジョー"),
            ("glowpad", "グロウパッド"),
            ("turret", "タレット"));

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(tempDirectory);
        MessagePatternTranslator.ResetForTests();
        MessagePatternTranslator.SetPatternFileForTests(patternFilePath);
    }

    [TearDown]
    public void TearDown()
    {
        MessagePatternTranslator.ResetForTests();
        Translator.ResetForTests();

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    // --- Status Predicate Family ---

    // Plain text (no color)
    [TestCase("The bear is exhausted!", "熊は疲弊した！")]
    [TestCase("The snapjaw is stunned!", "スナップジョーは気絶した！")]
    [TestCase("The bear is stuck.", "熊は動けなくなった。")]
    [TestCase("The glowpad is sealed.", "グロウパッドは封印された。")]
    [TestCase("You are exhausted!", "あなたは疲弊した！")]
    // Color-wrapped (AddPlayerMessage wraps entire message in {{color|...}})
    [TestCase("{{g|The bear is exhausted!}}", "{{g|熊は疲弊した！}}")]
    [TestCase("{{R|The snapjaw is stunned!}}", "{{R|スナップジョーは気絶した！}}")]
    public void Translate_StatusPredicateFamily(string input, string expected)
    {
        AssertTranslated(input, expected);
    }

    // --- Negation/Lack Family ---

    // Plain text
    [TestCase("The turret can't hear you!", "タレットにはあなたの声が聞こえない！")]
    [TestCase("The bear doesn't have a consciousness to appeal to.", "熊には訴えるべき意識がない。")]
    [TestCase("You don't penetrate the snapjaw's armor!", "スナップジョーの防具を貫通できなかった！")]
    [TestCase("You can't see!", "視界がない！")]
    [TestCase("The turret doesn't have enough charge to fire.", "タレットはfireするのに十分なchargeがない。")]
    // Color-wrapped (ConsequentialColor wraps full message)
    [TestCase("{{r|You don't penetrate the snapjaw's armor!}}", "{{r|スナップジョーの防具を貫通できなかった！}}")]
    public void Translate_NegationLackFamily(string input, string expected)
    {
        AssertTranslated(input, expected);
    }

    private static void AssertTranslated(string input, string expected)
    {
        var result = MessagePatternTranslator.Translate(input);
        Assert.That(result, Is.EqualTo(expected));
    }

    private void WriteExactDictionary(params (string key, string text)[] entries)
    {
        var builder = new StringBuilder();
        builder.Append("{\"entries\":[");
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

        builder.AppendLine("]}");
        File.WriteAllText(dictionaryPath, builder.ToString(), Utf8WithoutBom);
    }

    private static string EscapeJson(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }
}
