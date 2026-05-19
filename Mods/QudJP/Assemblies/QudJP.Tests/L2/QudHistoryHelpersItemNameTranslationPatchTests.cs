using System.Reflection;
using System.Text;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class QudHistoryHelpersItemNameTranslationPatchTests
{
    private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);

    private string tempDirectory = null!;
    private string dictionaryDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-history-item-name-l2", Guid.NewGuid().ToString("N"));
        dictionaryDirectory = Path.Combine(tempDirectory, "dict");
        Directory.CreateDirectory(dictionaryDirectory);

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(dictionaryDirectory);
        DynamicTextObservability.ResetForTests();
        RuntimeDiagnostics.SetVerboseProbesEnabledForTests(true);
        DummyQudHistoryHelpersTarget.HistoricItemNameResult = string.Empty;
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        DynamicTextObservability.ResetForTests();
        RuntimeDiagnostics.SetVerboseProbesEnabledForTests(null);
        DummyQudHistoryHelpersTarget.HistoricItemNameResult = string.Empty;

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [TestCase(nameof(DummyQudHistoryHelpersTarget.NameItem))]
    [TestCase(nameof(DummyQudHistoryHelpersTarget.NameItemNounRoot))]
    [TestCase(nameof(DummyQudHistoryHelpersTarget.NameItemAdjRoot))]
    public void Postfix_TranslatesGeneratedBlessingItemName_WhenPatched(string methodName)
    {
        WriteDictionaryFile("Scoped/historyspice-common.ja.json", ("sword", "剣"));
        WriteDictionaryFile("world-gospels.ja.json", ("blessing", "祝福"));
        DummyQudHistoryHelpersTarget.HistoricItemNameResult = "Sword's Blessing";
        var harmonyId = "qudjp-test-history-item-name-" + Guid.NewGuid().ToString("N");
        var harmony = new Harmony(harmonyId);
        try
        {
            var original = RequireDummyMethod(methodName);
            harmony.Patch(
                original: original,
                postfix: new HarmonyMethod(RequireMethod(
                    typeof(QudHistoryHelpersItemNameTranslationPatch),
                    nameof(QudHistoryHelpersItemNameTranslationPatch.Postfix),
                    typeof(MethodBase),
                    typeof(string).MakeByRefType())));

            var result = InvokeDummyMethod(methodName);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo("剣の祝福"));
                Assert.That(RouteHitCount(methodName), Is.EqualTo(1));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Postfix_LeavesUnknownItemNameUnchanged_WhenPatched()
    {
        DummyQudHistoryHelpersTarget.HistoricItemNameResult = "Swordicus";
        var harmonyId = "qudjp-test-history-item-name-" + Guid.NewGuid().ToString("N");
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireDummyMethod(nameof(DummyQudHistoryHelpersTarget.NameItem)),
                postfix: new HarmonyMethod(RequireMethod(
                    typeof(QudHistoryHelpersItemNameTranslationPatch),
                    nameof(QudHistoryHelpersItemNameTranslationPatch.Postfix),
                    typeof(MethodBase),
                    typeof(string).MakeByRefType())));

            var result = InvokeDummyMethod(nameof(DummyQudHistoryHelpersTarget.NameItem));

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo("Swordicus"));
                Assert.That(RouteHitCount(nameof(DummyQudHistoryHelpersTarget.NameItem)), Is.Zero);
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private void WriteDictionaryFile(string fileName, params (string Key, string Text)[] entries)
    {
        var path = Path.Combine(dictionaryDirectory, fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        var builder = new StringBuilder();
        builder.Append("{\"entries\":[");
        for (var index = 0; index < entries.Length; index++)
        {
            if (index > 0)
            {
                builder.Append(',');
            }

            builder.Append("{\"key\":\"");
            builder.Append(EscapeJson(entries[index].Key));
            builder.Append("\",\"text\":\"");
            builder.Append(EscapeJson(entries[index].Text));
            builder.Append("\"}");
        }

        builder.Append("]}");
        builder.AppendLine();
        File.WriteAllText(path, builder.ToString(), Utf8WithoutBom);
    }

    private static int RouteHitCount(string methodName) =>
        DynamicTextObservability.GetRouteFamilyHitCountForTests(
            nameof(QudHistoryHelpersItemNameTranslationPatch),
            QudHistoryHelpersItemNameTranslationPatch.FamilyFor(methodName));

    private static string InvokeDummyMethod(string methodName) =>
        (string)RequireDummyMethod(methodName).Invoke(null, new object?[] { "sword", new object(), new object() })!;

    private static MethodInfo RequireDummyMethod(string name) =>
        RequireMethod(typeof(DummyQudHistoryHelpersTarget), name, typeof(string), typeof(object), typeof(object));

    private static MethodInfo RequireMethod(Type type, string name, params Type[] parameters) =>
        AccessTools.Method(type, name, parameters)
        ?? throw new InvalidOperationException("Missing method " + type.FullName + "." + name);

    private static string EscapeJson(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
}
