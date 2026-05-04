using System.Reflection;
using System.Text;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class QudMutationsModuleWindowTranslationPatchTests
{
    private string tempRoot = null!;
    private string dictionariesDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempRoot = Path.Combine(Path.GetTempPath(), "qudjp-mutationwindow-l2", Guid.NewGuid().ToString("N"));
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
    public void Patch_TranslatesMutationMenuRowsBeforeShowAndUpdatesLongDescription()
    {
        WriteXmlFile(
            "Mutations.jp.xml",
            """
            <?xml version='1.0' encoding='utf-8'?>
            <mutations>
              <category Name="Physical" DisplayName="{{G|肉体突然変異}}">
                <mutation Name="Adrenal Control" DisplayName="アドレナリン制御" />
                <mutation Name="Stinger (Confusing Venom)" DisplayName="毒針（混乱毒）" />
              </category>
              <category Name="Mental" DisplayName="{{M|精神突然変異}}">
                <mutation Name="Esper" DisplayName="エスパー" />
              </category>
            </mutations>
            """);
        WriteDictionary(
            ("mutation:Esper", "精神突然変異しか発現しない。"),
            ("mutation:Adrenal Control", "アドレナリン分泌を制御できる。"),
            ("mutation:Adrenal Control:rank:1", "クールダウン: 200ターン"),
            ("mutation:Stinger (Confusing Venom)", "臀部の毒針を持つ。"),
            ("mutation:Stinger (Confusing Venom):Stinger Confusion:rank:1", "混乱毒を与える針攻撃。"));

        var harmonyId = $"qudjp.tests.{Guid.NewGuid():N}";
        var harmony = new Harmony(harmonyId);

        try
        {
            var transpiler = AccessTools.Method(
                typeof(QudMutationsModuleWindowTranslationPatch),
                "Transpiler",
                [typeof(IEnumerable<CodeInstruction>)]);
            Assert.That(transpiler, Is.Not.Null, "Transpiler method should be found");

            harmony.Patch(
                original: RequireMethod(typeof(DummyQudMutationsModuleWindow), nameof(DummyQudMutationsModuleWindow.UpdateControls)),
                transpiler: new HarmonyMethod(transpiler!),
                postfix: new HarmonyMethod(RequireMethod(typeof(QudMutationsModuleWindowTranslationPatch), nameof(QudMutationsModuleWindowTranslationPatch.Postfix))));

            var window = new DummyQudMutationsModuleWindow();
            window.UpdateControls();

            var esper = window.categoryMenus[0].menuOptions[0];
            var adrenalControl = window.categoryMenus[0].menuOptions[1];
            var stinger = window.categoryMenus[0].menuOptions[2];

            Assert.Multiple(() =>
            {
                Assert.That(window.prefabComponent.LastRenderedDescriptions, Is.EqualTo(new[]
                {
                    "エスパー",
                    "アドレナリン制御",
                    "毒針（混乱毒） [{{W|V}}]",
                }));
                Assert.That(esper.Description, Is.EqualTo("エスパー"));
                Assert.That(esper.LongDescription, Is.EqualTo("精神突然変異しか発現しない。"));
                Assert.That(adrenalControl.Description, Is.EqualTo("アドレナリン制御"));
                Assert.That(adrenalControl.LongDescription, Is.EqualTo("アドレナリン分泌を制御できる。\n\nクールダウン: 200ターン"));
                Assert.That(stinger.Description, Is.EqualTo("毒針（混乱毒） [{{W|V}}]"));
                Assert.That(stinger.LongDescription, Is.EqualTo("臀部の毒針を持つ。\n\n混乱毒を与える針攻撃。"));
                Assert.That(window.prefabComponent.LastRenderedLongDescriptions[2], Is.EqualTo("臀部の毒針を持つ。\n\n混乱毒を与える針攻撃。"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void TranslateFormattedDescription_ReturnsSource_ForMissingDictionaries()
    {
        Translator.SetDictionaryDirectoryForTests(Path.Combine(tempRoot, "missing-dictionaries"));

        var source = "Esper [{{W|V}}]";
        var translated = QudMutationsModuleWindowTranslationPatch.TranslateFormattedDescription(source);

        Assert.That(translated, Is.EqualTo(source));
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
            Path.Combine(dictionariesDirectory, "mutation-window-l2.ja.json"),
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
}
