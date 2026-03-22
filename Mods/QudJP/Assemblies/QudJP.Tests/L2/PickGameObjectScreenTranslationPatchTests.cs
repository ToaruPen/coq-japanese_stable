using System.Reflection;
using System.Text;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class PickGameObjectScreenTranslationPatchTests
{
    private string tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-pick-game-object-l2", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(tempDirectory);
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
    public void Postfix_TranslatesMenuOptionDescriptions_WhenUpdateViewRuns()
    {
        WriteDictionary(
            ("Close Menu", "メニューを閉じる"),
            ("navigate", "移動"),
            ("take all", "すべて取る"),
            ("store an item", "アイテムを収納"));

        var target = new DummyPickGameObjectScreen();

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyPickGameObjectScreen), nameof(DummyPickGameObjectScreen.UpdateViewFromData)),
                postfix: new HarmonyMethod(RequireMethod(typeof(PickGameObjectScreenTranslationPatch), nameof(PickGameObjectScreenTranslationPatch.Postfix))));

            target.UpdateViewFromData(reentry: false);

            Assert.Multiple(() =>
            {
                Assert.That(target.defaultMenuOptions[0].Description, Is.EqualTo("メニューを閉じる"));
                Assert.That(target.defaultMenuOptions[1].Description, Is.EqualTo("移動"));
                Assert.That(target.getItemMenuOptions[0].Description, Is.EqualTo("メニューを閉じる"));
                Assert.That(target.getItemMenuOptions[1].Description, Is.EqualTo("移動"));
                Assert.That(target.TAKE_ALL.Description, Is.EqualTo("すべて取る"));
                Assert.That(target.STORE_ITEM.Description, Is.EqualTo("アイテムを収納"));
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

        var path = Path.Combine(tempDirectory, "ui-pick-game-object.ja.json");
        File.WriteAllText(path, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static string EscapeJson(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }
}
