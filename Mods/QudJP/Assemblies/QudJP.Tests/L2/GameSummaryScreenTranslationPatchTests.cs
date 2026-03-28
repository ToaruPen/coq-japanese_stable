using System.Reflection;
using System.Text;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class GameSummaryScreenTranslationPatchTests
{
    private string tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-game-summary-screen-l2", Guid.NewGuid().ToString("N"));
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
    public void Postfix_TranslatesKeyMenuOptionDescriptions()
    {
        WriteDictionary(
            ("Save Tombstone File", "墓碑ファイルを保存"),
            ("Exit", "終了"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        var screen = new DummyGameSummaryScreen();
        screen.keyMenuOptions.Add(new DummyMenuOption("Save Tombstone File", keyDescription: "F2"));
        screen.keyMenuOptions.Add(new DummyMenuOption("Exit", keyDescription: "Esc"));

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyGameSummaryScreen), nameof(DummyGameSummaryScreen.UpdateMenuBars)),
                postfix: new HarmonyMethod(RequireMethod(typeof(GameSummaryScreenTranslationPatch), nameof(GameSummaryScreenTranslationPatch.Postfix), typeof(object))));

            screen.UpdateMenuBars();

            Assert.Multiple(() =>
            {
                Assert.That(screen.keyMenuOptions[0].Description, Is.EqualTo("墓碑ファイルを保存"));
                Assert.That(screen.keyMenuOptions[1].Description, Is.EqualTo("終了"));
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

    private static MethodInfo RequireMethod(Type type, string methodName, params Type[] parameterTypes)
    {
        if (parameterTypes.Length == 0)
        {
            return AccessTools.Method(type, methodName)
                ?? throw new InvalidOperationException($"Method not found: {type.FullName}.{methodName}");
        }

        return AccessTools.Method(type, methodName, parameterTypes)
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
            Path.Combine(tempDirectory, "game-summary-screen.ja.json"),
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
