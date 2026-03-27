using System.Reflection;
using System.Text;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class OptionsLocalizationPatchTests
{
    private string tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-options-l2", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(tempDirectory);
        SinkObservation.ResetForTests();
        DummyOptionsTarget.ResetDefaults();
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        SinkObservation.ResetForTests();

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    public void Postfix_ObservationOnly_LeavesOptionRowsUnchanged_WhenPatched()
    {
        WriteDictionary(
            ("Main volume", "主音量"),
            ("Adjust slider", "スライダーを調整"));

        var target = new DummyOptionsTarget();
        target.menuItems.Add(new DummyOptionsRow("{{W|Main volume}}", "Adjust slider"));
        target.filteredMenuItems.Add(new DummyOptionsRow("Main volume", "{{R|Adjust slider}}"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyOptionsTarget), nameof(DummyOptionsTarget.Show)),
                postfix: new HarmonyMethod(RequireMethod(typeof(OptionsLocalizationPatch), nameof(OptionsLocalizationPatch.Postfix))));

            target.Show();

            Assert.Multiple(() =>
            {
                Assert.That(target.menuItems[0].Title, Is.EqualTo("{{W|Main volume}}"));
                Assert.That(target.menuItems[0].HelpText, Is.EqualTo("Adjust slider"));
                Assert.That(target.filteredMenuItems[0].Title, Is.EqualTo("Main volume"));
                Assert.That(target.filteredMenuItems[0].HelpText, Is.EqualTo("{{R|Adjust slider}}"));
                Assert.That(
                    SinkObservation.GetHitCountForTests(
                        nameof(UITextSkinTranslationPatch),
                        nameof(OptionsLocalizationPatch),
                        SinkObservation.ObservationOnlyDetail,
                        "{{W|Main volume}}",
                        "Main volume"),
                    Is.GreaterThan(0));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Postfix_ObservationOnly_LeavesDefaultMenuDescriptionsUnchanged_WhenPatched()
    {
        WriteDictionary(
            ("Collapse All", "すべてたたむ"),
            ("Help", "ヘルプ"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyOptionsTarget), nameof(DummyOptionsTarget.Show)),
                postfix: new HarmonyMethod(RequireMethod(typeof(OptionsLocalizationPatch), nameof(OptionsLocalizationPatch.Postfix))));

            new DummyOptionsTarget().Show();

            Assert.Multiple(() =>
            {
                Assert.That(DummyOptionsTarget.defaultMenuOptions[0].Description, Is.EqualTo("Collapse All"));
                Assert.That(DummyOptionsTarget.defaultMenuOptions[1].Description, Is.EqualTo("Help"));
                Assert.That(
                    SinkObservation.GetHitCountForTests(
                        nameof(UITextSkinTranslationPatch),
                        nameof(OptionsLocalizationPatch),
                        SinkObservation.ObservationOnlyDetail,
                        "Collapse All",
                        "Collapse All"),
                    Is.GreaterThan(0));
            });
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

        var path = Path.Combine(tempDirectory, "ui-options.ja.json");
        File.WriteAllText(path, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static string EscapeJson(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }
}
