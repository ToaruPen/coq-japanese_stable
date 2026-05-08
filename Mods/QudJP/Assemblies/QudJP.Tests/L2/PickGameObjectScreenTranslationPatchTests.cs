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
    public void Prefix_TranslatesMenuOptionDescriptions_BeforeBinding_WhenUpdateViewRuns()
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
                prefix: new HarmonyMethod(RequireMethod(typeof(PickGameObjectScreenTranslationPatch), nameof(PickGameObjectScreenTranslationPatch.Prefix))));

            target.UpdateViewFromData(reentry: false);

            Assert.Multiple(() =>
            {
                Assert.That(target.renderedDefaultMenuOptions[0].Description, Is.EqualTo("メニューを閉じる"));
                Assert.That(target.renderedDefaultMenuOptions[1].Description, Is.EqualTo("移動"));
                Assert.That(target.renderedGetItemMenuOptions[0].Description, Is.EqualTo("メニューを閉じる"));
                Assert.That(target.renderedGetItemMenuOptions[1].Description, Is.EqualTo("移動"));
                Assert.That(target.renderedTakeAll?.Description, Is.EqualTo("すべて取る"));
                Assert.That(target.renderedStoreItem?.Description, Is.EqualTo("アイテムを収納"));
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(PickGameObjectScreenTranslationPatch),
                        "PickGameObject.Description"),
                    Is.GreaterThan(0));
                Assert.That(
                    SinkObservation.GetHitCountForTests(
                        nameof(UITextSkinTranslationPatch),
                        nameof(PickGameObjectScreenTranslationPatch),
                        SinkObservation.ObservationOnlyDetail,
                        "Close Menu",
                        "Close Menu"),
                    Is.EqualTo(0));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void PickItemTitlePrefix_TranslatesGetItemDialogContainerTitle()
    {
        var title = "{{W|Opening a チェスト}}";

        PickItemShowPickerTitleTranslationPatch.Prefix(
            DummyPickItemDialogStyle.GetItemDialog,
            new object(),
            ref title);

        Assert.Multiple(() =>
        {
            Assert.That(title, Is.EqualTo("{{W|チェストを開いています}}"));
            Assert.That(
                DynamicTextObservability.GetRouteFamilyHitCountForTests(
                    nameof(PickItemShowPickerTitleTranslationPatch),
                    "PickItem.ContainerTitle"),
                Is.EqualTo(1));
        });
    }

    [Test]
    public void PickItemTitlePrefix_LeavesNonGetItemDialogOpeningTextUnchanged()
    {
        var title = "Opening the ark will expose the core to outside influence.";

        PickItemShowPickerTitleTranslationPatch.Prefix(
            DummyPickItemDialogStyle.SelectItemDialog,
            new object(),
            ref title);

        Assert.That(title, Is.EqualTo("Opening the ark will expose the core to outside influence."));
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
