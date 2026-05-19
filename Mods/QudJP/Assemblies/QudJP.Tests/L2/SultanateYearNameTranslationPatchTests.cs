using System.Reflection;
using System.Text;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class SultanateYearNameTranslationPatchTests
{
    private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);

    private string tempDirectory = null!;
    private string dictionaryDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-sultanate-year-name-l2", Guid.NewGuid().ToString("N"));
        dictionaryDirectory = Path.Combine(tempDirectory, "dict");
        Directory.CreateDirectory(dictionaryDirectory);

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(dictionaryDirectory);
        DynamicTextObservability.ResetForTests();
        RuntimeDiagnostics.SetVerboseProbesEnabledForTests(true);
        DummyQudHistoryHelpersTarget.SultanateYearNameResult = string.Empty;
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        DynamicTextObservability.ResetForTests();
        RuntimeDiagnostics.SetVerboseProbesEnabledForTests(null);
        DummyQudHistoryHelpersTarget.SultanateYearNameResult = string.Empty;

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    public void Postfix_TranslatesSultanateYearName_WhenPatched()
    {
        WriteDictionaryFile("Scoped/historyspice-common.ja.json", ("shining", "輝く"), ("visage", "容貌"));
        DummyQudHistoryHelpersTarget.SultanateYearNameResult = "Year of the Shining Visage";
        var harmonyId = "qudjp-test-sultanate-year-name-" + Guid.NewGuid().ToString("N");
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyQudHistoryHelpersTarget), nameof(DummyQudHistoryHelpersTarget.GenerateSultanateYearName)),
                postfix: new HarmonyMethod(RequireMethod(typeof(SultanateYearNameTranslationPatch), nameof(SultanateYearNameTranslationPatch.Postfix), typeof(string).MakeByRefType())));

            var result = DummyQudHistoryHelpersTarget.GenerateSultanateYearName();

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo("輝く容貌の年"));
                Assert.That(RouteHitCount(), Is.EqualTo(1));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Postfix_LeavesUnknownSultanateYearNameUnchanged_WhenPatched()
    {
        DummyQudHistoryHelpersTarget.SultanateYearNameResult = "Year of the Unknown Visage";
        var harmonyId = "qudjp-test-sultanate-year-name-" + Guid.NewGuid().ToString("N");
        var harmony = new Harmony(harmonyId);
        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyQudHistoryHelpersTarget), nameof(DummyQudHistoryHelpersTarget.GenerateSultanateYearName)),
                postfix: new HarmonyMethod(RequireMethod(typeof(SultanateYearNameTranslationPatch), nameof(SultanateYearNameTranslationPatch.Postfix), typeof(string).MakeByRefType())));

            var result = DummyQudHistoryHelpersTarget.GenerateSultanateYearName();

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo("Year of the Unknown Visage"));
                Assert.That(RouteHitCount(), Is.Zero);
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

    private static int RouteHitCount() =>
        DynamicTextObservability.GetRouteFamilyHitCountForTests(
            nameof(SultanateYearNameTranslationPatch),
            SultanateYearNameTranslationPatch.Family);

    private static MethodInfo RequireMethod(Type type, string name, params Type[] parameters) =>
        AccessTools.Method(type, name, parameters)
        ?? throw new InvalidOperationException("Missing method " + type.FullName + "." + name);

    private static string EscapeJson(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
}
