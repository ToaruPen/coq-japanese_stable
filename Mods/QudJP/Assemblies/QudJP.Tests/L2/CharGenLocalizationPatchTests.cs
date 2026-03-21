using System.Reflection;
using System.Text;
using HarmonyLib;
using QudJP.Patches;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class CharGenLocalizationPatchTests
{
    private string tempRoot = null!;
    private string dictionariesDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempRoot = Path.Combine(Path.GetTempPath(), "qudjp-chargen-l2", Guid.NewGuid().ToString("N"));
        dictionariesDirectory = Path.Combine(tempRoot, "Dictionaries");
        Directory.CreateDirectory(dictionariesDirectory);

        LocalizationAssetResolver.SetLocalizationRootForTests(tempRoot);
        Translator.SetDictionaryDirectoryForTests(dictionariesDirectory);
        ChargenStructuredTextTranslator.ResetForTests();
    }

    [TearDown]
    public void TearDown()
    {
        ChargenStructuredTextTranslator.ResetForTests();
        Translator.ResetForTests();
        LocalizationAssetResolver.SetLocalizationRootForTests(null);

        if (Directory.Exists(tempRoot))
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Test]
    public void Postfix_TranslatesCallingDescriptionFromStructuredAssets()
    {
        WriteXmlFile(
            "Factions.jp.xml",
            """
            <?xml version='1.0' encoding='utf-8'?>
            <factions Load="Merge">
              <faction Name="Bears" DisplayName="クマ" />
            </factions>
            """);
        WriteDictionary(
            ("Persuasion", "説得術"),
            ("Intimidate", "威圧"),
            ("Starts with random junk and artifacts", "ランダムなガラクタとアーティファクトを所持して開始"));

        RunWithPostfixPatch(() =>
        {
            var source = """
                {{c|ù}} Persuasion
                  {{C|ù}} Intimidate
                {{c|ù}} +2 Ego
                {{c|ù}} +100 reputation with Bears
                {{c|ù}} Starts with random junk and artifacts
                """;

            var result = new DummyCharGenTextSource(source).GetDescriptionText();

            var expected = """
                {{c|ù}} 説得術
                  {{C|ù}} 威圧
                {{c|ù}} 自我 +2
                {{c|ù}} クマとの評判 +100
                {{c|ù}} ランダムなガラクタとアーティファクトを所持して開始
                """;

            Assert.That(result, Is.EqualTo(expected));
        });
    }

    [Test]
    public void Postfix_TranslatesMutationOptionFromMutationsXml()
    {
        WriteXmlFile(
            "Mutations.jp.xml",
            """
            <?xml version='1.0' encoding='utf-8'?>
            <mutations>
              <category Name="Physical" DisplayName="{{G|肉体突然変異}}">
                <mutation Name="Adrenal Control" DisplayName="アドレナリン制御" />
              </category>
            </mutations>
            """);

        RunWithPostfixPatch(() =>
        {
            var result = new DummyCharGenTextSource("Adrenal Control").GetDescriptionText();

            Assert.That(result, Is.EqualTo("アドレナリン制御"));
        });
    }

    [Test]
    public void Postfix_TranslatesPointsRemainingChrome()
    {
        WriteDictionary(("Points Remaining:", "残りポイント:"));

        RunWithPostfixPatch(() =>
        {
            var result = new DummyCharGenTextSource("Points Remaining: 12").GetDescriptionText();

            Assert.That(result, Is.EqualTo("残りポイント: 12"));
        });
    }

    [Test]
    public void Postfix_TranslatesRawBulletCallingDescriptionFromStructuredAssets()
    {
        WriteXmlFile(
            "Factions.jp.xml",
            """
            <?xml version='1.0' encoding='utf-8'?>
            <factions Load="Merge">
              <faction Name="Issachari" DisplayName="イッサカリ族" />
            </factions>
            """);
        WriteDictionary(
            ("Wayfaring", "サバイバル"),
            ("Wilderness Lore: Random", "荒地巡り：ランダム"),
            ("Starts with random junk and artifacts", "ランダムなガラクタとアーティファクトを所持して開始"));

        RunWithPostfixPatch(() =>
        {
            var source = """
                ù +2 Toughness
                ù Wayfaring
                  ù Wilderness Lore: Random
                ù +400 reputation with the Issachari
                ù Starts with random junk and artifacts
                """;

            var result = new DummyCharGenTextSource(source).GetDescriptionText();

            var expected = """
                ù 頑健 +2
                ù サバイバル
                  ù 荒地巡り：ランダム
                ù イッサカリ族との評判 +400
                ù ランダムなガラクタとアーティファクトを所持して開始
                """;

            Assert.That(result, Is.EqualTo(expected));
        });
    }

    private static void RunWithPostfixPatch(Action assertion)
    {
        var harmonyId = $"qudjp.tests.{Guid.NewGuid():N}";
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyCharGenTextSource), nameof(DummyCharGenTextSource.GetDescriptionText)),
                postfix: new HarmonyMethod(RequireMethod(typeof(CharGenLocalizationPatch), nameof(CharGenLocalizationPatch.Postfix))));
            assertion();
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
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
            Path.Combine(dictionariesDirectory, "chargen-l2.ja.json"),
            builder.ToString(),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private void WriteXmlFile(string relativePath, string content)
    {
        File.WriteAllText(
            Path.Combine(tempRoot, relativePath),
            content.ReplaceLineEndings(Environment.NewLine),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
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

    private sealed class DummyCharGenTextSource
    {
        private readonly string text;

        public DummyCharGenTextSource(string text)
        {
            this.text = text;
        }

        public string GetDescriptionText()
        {
            return text;
        }
    }
}
