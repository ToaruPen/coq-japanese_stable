using System.Reflection;
using System.Text;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class SteamWorkshopUploaderViewTranslationPatchTests
{
    private string tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-steam-workshop-uploader-l2", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(tempDirectory);
        DynamicTextObservability.ResetForTests();
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        DynamicTextObservability.ResetForTests();

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    public void PopupAndProgressMethods_TranslateText_WhenPatched()
    {
        WriteDictionary(
            ("Committing changes...", "変更を確定中…"),
            ("Submitting update... Please wait.", "更新を送信中…お待ちください。"),
            ("SUCCESS! Item submitted!", "成功！アイテムが送信されました！"),
            ("Error: I/O Failure!", "エラー: I/O失敗!"));

        var harmonyId = $"qudjp.tests.{Guid.NewGuid():N}";
        var harmony = new Harmony(harmonyId);
        try
        {
            var prefix = new HarmonyMethod(RequireMethod(typeof(SteamWorkshopUploaderViewTranslationPatch), nameof(SteamWorkshopUploaderViewTranslationPatch.Prefix)));
            harmony.Patch(
                original: RequireMethod(typeof(DummySteamWorkshopUploaderViewTarget), nameof(DummySteamWorkshopUploaderViewTarget.ShowProgress)),
                prefix: prefix);
            harmony.Patch(
                original: RequireMethod(typeof(DummySteamWorkshopUploaderViewTarget), nameof(DummySteamWorkshopUploaderViewTarget.SetProgress)),
                prefix: prefix);
            harmony.Patch(
                original: RequireMethod(typeof(DummySteamWorkshopUploaderViewTarget), nameof(DummySteamWorkshopUploaderViewTarget.Popup)),
                prefix: prefix);

            var target = new DummySteamWorkshopUploaderViewTarget();
            target.ShowProgress("Submitting update... Please wait.");
            target.SetProgress("Committing changes...", 50f);
            target.Popup("SUCCESS! Item submitted!");

            Assert.Multiple(() =>
            {
                Assert.That(target.LastProgressText, Is.EqualTo("変更を確定中…"));
                Assert.That(target.LastProgressValue, Is.EqualTo(50f));
                Assert.That(target.LastPopup, Is.EqualTo("成功！アイテムが送信されました！"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(nameof(SteamWorkshopUploaderViewTranslationPatch), "SteamWorkshopUploaderView.Text"),
                    Is.GreaterThan(0));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
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
            Path.Combine(tempDirectory, "steam-workshop-uploader-l2.ja.json"),
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
