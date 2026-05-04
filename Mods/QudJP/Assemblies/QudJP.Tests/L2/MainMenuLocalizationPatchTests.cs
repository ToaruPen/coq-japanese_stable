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
        SinkObservation.ResetForTests();
        DummyMainMenuTarget.ResetDefaults();
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

            Assert.Multiple(() =>
            {
                Assert.That(DummyMainMenuTarget.LeftOptions[1].Text, Is.EqualTo("{{G|モッド}}"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Postfix_DoesNotRetranslateModMenuLabel_WhenMainMenuRefreshes()
    {
        WriteDictionary(
            ("Mods", "Mod"),
            ("Mod", "改造"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyMainMenuTarget), nameof(DummyMainMenuTarget.Show)),
                postfix: new HarmonyMethod(RequireMethod(typeof(MainMenuLocalizationPatch), nameof(MainMenuLocalizationPatch.Postfix))));

            var target = new DummyMainMenuTarget();
            target.Show();
            target.Show();

            Assert.That(DummyMainMenuTarget.LeftOptions[1].Text, Is.EqualTo("Mod"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Postfix_TranslatesHotkeyBarDescriptions_WhenUpdateMenuBarsRuns()
    {
        WriteDictionary(
            ("navigate", "移動"),
            ("select", "選択"),
            ("quit", "終了"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyMainMenuTarget), nameof(DummyMainMenuTarget.UpdateMenuBars)),
                postfix: new HarmonyMethod(RequireMethod(typeof(MainMenuLocalizationPatch), nameof(MainMenuLocalizationPatch.Postfix))));

            var target = new DummyMainMenuTarget();
            target.UpdateMenuBars();

            Assert.Multiple(() =>
            {
                Assert.That(DummyMainMenuTarget.LastHotkeyChoices[0].Description, Is.EqualTo("移動"));
                Assert.That(DummyMainMenuTarget.LastHotkeyChoices[1].Description, Is.EqualTo("選択"));
                Assert.That(DummyMainMenuTarget.LastHotkeyChoices[2].Description, Is.EqualTo("終了"));
                Assert.That(target.hotkeyBar.renderedDescriptions, Is.EqualTo(new[] { "移動", "選択", "終了" }));
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
