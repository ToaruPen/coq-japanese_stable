using System.Reflection;
using System.Text;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class MainMenuLocalizationPatchTests
{
    private string tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-mainmenu-l2", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(tempDirectory);
        DummyMainMenuTarget.ResetDefaults();
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
    public void Postfix_TranslatesMenuOptions_WhenPatched()
    {
        WriteDictionary(
            ("Options", "設定"),
            ("Help", "ヘルプ"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyMainMenuTarget), nameof(DummyMainMenuTarget.Show)),
                postfix: new HarmonyMethod(RequireMethod(typeof(MainMenuLocalizationPatch), nameof(MainMenuLocalizationPatch.Postfix))));

            new DummyMainMenuTarget().Show();

            Assert.Multiple(() =>
            {
                Assert.That(DummyMainMenuTarget.LeftOptions[0].Text, Is.EqualTo("設定"));
                Assert.That(DummyMainMenuTarget.RightOptions[0].Text, Is.EqualTo("ヘルプ"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Postfix_TranslatesMarkupWrappedText_WhenPatched()
    {
        WriteDictionary(("Mods", "モッド"));

        DummyMainMenuTarget.LeftOptions[1].Text = "{{G|Mods}}";

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyMainMenuTarget), nameof(DummyMainMenuTarget.Show)),
                postfix: new HarmonyMethod(RequireMethod(typeof(MainMenuLocalizationPatch), nameof(MainMenuLocalizationPatch.Postfix))));

            new DummyMainMenuTarget().Show();

            Assert.That(DummyMainMenuTarget.LeftOptions[1].Text, Is.EqualTo("{{G|モッド}}"));
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

        var path = Path.Combine(tempDirectory, "ui-mainmenu.ja.json");
        File.WriteAllText(path, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static string EscapeJson(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }
}
