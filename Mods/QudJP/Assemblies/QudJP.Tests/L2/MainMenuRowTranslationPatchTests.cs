using System.Reflection;
using System.Text;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class MainMenuRowTranslationPatchTests
{
    private string tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-mainmenu-row-l2", Guid.NewGuid().ToString("N"));
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
    public void Prefix_TranslatesRowText_BeforeUnityBinding_WhenPatched()
    {
        WriteDictionary(("Options", "設定"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyMainMenuRow), nameof(DummyMainMenuRow.setData)),
                prefix: new HarmonyMethod(RequireMethod(typeof(MainMenuRowTranslationPatch), nameof(MainMenuRowTranslationPatch.Prefix))));

            var row = new DummyMainMenuRow();
            row.setData(new DummyMainMenuOption("Options", "Pick:Options"));

            Assert.Multiple(() =>
            {
                Assert.That(row.text.text, Is.EqualTo("設定"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(MainMenuRowTranslationPatch),
                        "MainMenu.Text"),
                    Is.GreaterThan(0));
                Assert.That(
                    SinkObservation.GetHitCountForTests(
                        nameof(UITextSkinTranslationPatch),
                        nameof(MainMenuRowTranslationPatch),
                        SinkObservation.ObservationOnlyDetail,
                        "Options",
                        "Options"),
                    Is.EqualTo(0));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Prefix_DoesNotRetranslateModMenuLabel_WhenRowDataIsRebound()
    {
        WriteDictionary(
            ("Mods", "Mod"),
            ("Mod", "改造"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyMainMenuRow), nameof(DummyMainMenuRow.setData)),
                prefix: new HarmonyMethod(RequireMethod(typeof(MainMenuRowTranslationPatch), nameof(MainMenuRowTranslationPatch.Prefix))));

            var option = new DummyMainMenuOption("Mods", "Pick:Installed Mod Configuration");
            var row = new DummyMainMenuRow();
            row.setData(option);
            row.setData(option);

            Assert.Multiple(() =>
            {
                Assert.That(option.Text, Is.EqualTo("Mod"));
                Assert.That(row.text.text, Is.EqualTo("Mod"));
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
        File.WriteAllText(
            Path.Combine(tempDirectory, "main-menu-row-l2.ja.json"),
            builder.ToString(),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static string EscapeJson(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }
}
