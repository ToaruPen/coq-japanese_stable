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
        ScopedDictionaryLookup.ResetForTests();
        DynamicTextObservability.ResetForTests();
        RuntimeDiagnostics.SetVerboseProbesEnabledForTests(true);
        DummyQudHistoryHelpersTarget.HistoricItemNameResult = string.Empty;
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        ScopedDictionaryLookup.ResetForTests();
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

        var result = InvokeWithPatchedPostfix(methodName);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo("剣の祝福"));
            Assert.That(RouteHitCount(methodName), Is.EqualTo(1));
        });
    }

    [Test]
    public void Postfix_LeavesUnknownItemNameUnchanged_WhenPatched()
    {
        WriteDictionaryFile("Scoped/historyspice-common.ja.json", ("sword", "剣"));
        WriteDictionaryFile("world-gospels.ja.json", ("blessing", "祝福"));
        DummyQudHistoryHelpersTarget.HistoricItemNameResult = "Swordicus";
        var result = InvokeWithPatchedPostfix(nameof(DummyQudHistoryHelpersTarget.NameItem));

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo("Swordicus"));
            Assert.That(RouteHitCount(nameof(DummyQudHistoryHelpersTarget.NameItem)), Is.Zero);
        });
    }

    [Test]
    public void Postfix_LeavesEmptyItemNameUnchanged_WhenPatched()
    {
        WriteDictionaryFile("Scoped/historyspice-common.ja.json", ("sword", "剣"));
        WriteDictionaryFile("world-gospels.ja.json", ("blessing", "祝福"));
        DummyQudHistoryHelpersTarget.HistoricItemNameResult = string.Empty;

        var result = InvokeWithPatchedPostfix(nameof(DummyQudHistoryHelpersTarget.NameItem));

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Empty);
            Assert.That(RouteHitCount(nameof(DummyQudHistoryHelpersTarget.NameItem)), Is.Zero);
        });
    }

    [Test]
    public void Postfix_PreservesColorTagsOnGeneratedItemName_WhenPatched()
    {
        WriteDictionaryFile("Scoped/historyspice-common.ja.json", ("sword", "剣"));
        WriteDictionaryFile("world-gospels.ja.json", ("blessing", "祝福"));
        DummyQudHistoryHelpersTarget.HistoricItemNameResult = "<color=#44ff88>Sword's Blessing</color>";

        var result = InvokeWithPatchedPostfix(nameof(DummyQudHistoryHelpersTarget.NameItem));

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo("<color=#44ff88>剣の祝福</color>"));
            Assert.That(RouteHitCount(nameof(DummyQudHistoryHelpersTarget.NameItem)), Is.EqualTo(1));
        });
    }

    [Test]
    public void Postfix_StripsDirectMarkerWithoutRetranslating_WhenPatched()
    {
        WriteDictionaryFile("Scoped/historyspice-common.ja.json", ("sword", "剣"));
        WriteDictionaryFile("world-gospels.ja.json", ("blessing", "祝福"));
        DummyQudHistoryHelpersTarget.HistoricItemNameResult =
            MessageFrameTranslator.DirectTranslationMarker + "Sword's Blessing";

        var result = InvokeWithPatchedPostfix(nameof(DummyQudHistoryHelpersTarget.NameItem));

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo("Sword's Blessing"));
            Assert.That(RouteHitCount(nameof(DummyQudHistoryHelpersTarget.NameItem)), Is.EqualTo(1));
        });
    }

    private static string InvokeWithPatchedPostfix(string methodName)
    {
        var harmonyId = "qudjp-test-history-item-name-" + Guid.NewGuid().ToString("N");
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireDummyMethod(methodName),
                postfix: new HarmonyMethod(RequireMethod(
                    typeof(QudHistoryHelpersItemNameTranslationPatch),
                    nameof(QudHistoryHelpersItemNameTranslationPatch.Postfix),
                    typeof(MethodBase),
                    typeof(string).MakeByRefType())));

            return InvokeDummyMethod(methodName);
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
