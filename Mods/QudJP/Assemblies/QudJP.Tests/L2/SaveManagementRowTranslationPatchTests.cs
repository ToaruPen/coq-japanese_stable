using System.Reflection;
using System.Text;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class SaveManagementRowTranslationPatchTests
{
    private string tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-save-management-row-l2", Guid.NewGuid().ToString("N"));
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
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    public void Postfix_TranslatesLastSavedLine_WhenPatched()
    {
        WriteDictionary(("Last saved:", "最終セーブ："));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummySaveManagementRowTarget), nameof(DummySaveManagementRowTarget.setData)),
                postfix: new HarmonyMethod(RequireMethod(typeof(SaveManagementRowTranslationPatch), nameof(SaveManagementRowTranslationPatch.Postfix))));

            var target = new DummySaveManagementRowTarget();
            target.setData(new DummySaveInfoData());

            const string source = "{{C|Last saved:}} 1 hour ago";
            Assert.Multiple(() =>
            {
                Assert.That(target.TextSkins[2].Text, Is.EqualTo("{{C|最終セーブ：}} 1 hour ago"));
                Assert.That(target.TextSkins[3].Text, Is.EqualTo("{{K|Total size: 12mb {save-123} }}"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(SaveManagementRowTranslationPatch),
                        "SaveManagementRow.LastSaved"),
                    Is.GreaterThan(0));
                Assert.That(
                    SinkObservation.GetHitCountForTests(
                        nameof(UITextSkinTranslationPatch),
                        nameof(SaveManagementRowTranslationPatch),
                        SinkObservation.ObservationOnlyDetail,
                        source,
                        "Last saved: 1 hour ago"),
                    Is.EqualTo(0));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Postfix_LeavesLastSavedUnchanged_WhenTranslationIsMissing()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummySaveManagementRowTarget), nameof(DummySaveManagementRowTarget.setData)),
                postfix: new HarmonyMethod(RequireMethod(typeof(SaveManagementRowTranslationPatch), nameof(SaveManagementRowTranslationPatch.Postfix))));

            var target = new DummySaveManagementRowTarget();
            target.setData(new DummySaveInfoData());

            Assert.That(target.TextSkins[2].Text, Is.EqualTo("{{C|Last saved:}} 1 hour ago"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
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
        builder.Append('{');
        builder.Append("\"entries\":[");

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
            Path.Combine(tempDirectory, "save-management-row-l2.ja.json"),
            builder.ToString(),
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
