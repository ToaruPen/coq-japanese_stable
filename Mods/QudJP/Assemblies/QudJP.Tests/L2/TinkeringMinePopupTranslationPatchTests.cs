using System.Reflection;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class TinkeringMinePopupTranslationPatchTests
{
    private string tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-tinkering-mine-l2", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        File.WriteAllText(Path.Combine(tempDirectory, "empty.ja.json"), "{\"entries\":[]}");
        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(tempDirectory);
        DynamicTextObservability.ResetForTests();
        DummyPopupShow.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        DummyPopupShow.Reset();
        DynamicTextObservability.ResetForTests();
        Translator.ResetForTests();
        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [TestCase(
        "Failing to disarm the {{R|フラッシュバンググレネード mk I mine}} will detonate it. You estimate you have about a {{G|90%}} chance of success. Do you want to make the attempt?",
        "{{R|フラッシュバンググレネード mk I mine}}の解除に失敗すると爆発する。成功率はおよそ{{G|90%}}だと見積もっている。試みますか？")]
    [TestCase(
        "Failing to disarm {{R|フラッシュバンググレネード mk I mine}} will detonate it. You estimate you have less than a {{R|20%}} chance of success. Do you want to make the attempt?",
        "{{R|フラッシュバンググレネード mk I mine}}の解除に失敗すると爆発する。成功率は{{R|20%}}未満だと見積もっている。試みますか？")]
    public void HandleEvent_TranslatesDisarmConfirmationPopup_WhenOwnerPatched(string source, string expected)
    {
        WithPatchedPopupOwnerIfAvailable(() =>
        {
            var target = new DummyTinkeringMinePopupTarget { PopupMessageToShow = source };

            target.HandleEvent(new DummyInventoryActionEvent());

            Assert.That(DummyPopupShow.LastShowYesNoMessage, Is.EqualTo(expected));
        });
    }

    [Test]
    public void HandleEvent_TranslatesGeneratedMineNameCapture_WhenOwnerPatched()
    {
        WriteDictionaryFile(
            "ui-displayname-atomic.ja.json",
            ("flashbang grenade mk I mine", "フラッシュバンググレネード mk I 地雷"));

        WithPatchedPopupOwnerIfAvailable(() =>
        {
            var target = new DummyTinkeringMinePopupTarget
            {
                PopupMessageToShow = "Failing to disarm the {{R|flashbang grenade mk I mine}} will detonate it. You estimate you have about a {{G|90%}} chance of success. Do you want to make the attempt?",
            };

            target.HandleEvent(new DummyInventoryActionEvent());

            Assert.That(
                DummyPopupShow.LastShowYesNoMessage,
                Is.EqualTo("{{R|フラッシュバンググレネード mk I 地雷}}の解除に失敗すると爆発する。成功率はおよそ{{G|90%}}だと見積もっている。試みますか？"));
        });
    }

    [Test]
    public void HandleEvent_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent()
    {
        const string source = "Failing to disarm the {{R|フラッシュバンググレネード mk I mine}} will detonate it. You estimate you have about a {{G|90%}} chance of success. Do you want to make the attempt?";

        OwnerPopupRouteTestHarness.WithPatchedPopupOnly(() => DummyPopupShow.ShowYesNo(source));

        Assert.That(DummyPopupShow.LastShowYesNoMessage, Is.EqualTo(source));
    }

    private static void WithPatchedPopupOwnerIfAvailable(Action action)
    {
        var harmonyId = $"qudjp.tests.{Guid.NewGuid():N}";
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchPopupShowYesNo(harmony);
            var patchType = typeof(PopupShowSemanticPipeline).Assembly.GetType("QudJP.Patches.TinkeringMinePopupTranslationPatch");
            var ownerMethod = RequireMethod(typeof(DummyTinkeringMinePopupTarget), nameof(DummyTinkeringMinePopupTarget.HandleEvent));
            if (patchType is not null)
            {
                var prefix = AccessTools.Method(patchType, "Prefix");
                var finalizer = AccessTools.Method(patchType, "Finalizer", [typeof(Exception)]);
                harmony.Patch(
                    original: ownerMethod,
                    prefix: prefix is null ? null : new HarmonyMethod(prefix),
                    finalizer: finalizer is null ? null : new HarmonyMethod(finalizer));
            }

            action();
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void PatchPopupShowYesNo(Harmony harmony)
    {
        harmony.Patch(
            original: RequireMethod(typeof(DummyPopupShow), nameof(DummyPopupShow.ShowYesNo)),
            prefix: new HarmonyMethod(RequireMethod(typeof(PopupShowTranslationPatch), nameof(PopupShowTranslationPatch.Prefix))),
            finalizer: new HarmonyMethod(RequireMethod(typeof(PopupShowTranslationPatch), nameof(PopupShowTranslationPatch.Finalizer))));
    }

    private static MethodInfo RequireMethod(Type type, string methodName)
    {
        return type.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)
               ?? throw new InvalidOperationException($"Method not found: {type.FullName}.{methodName}");
    }

    private void WriteDictionaryFile(string fileName, params (string key, string text)[] entries)
    {
        var contents = "{\"entries\":["
            + string.Join(
                ",",
                entries.Select(entry => $"{{\"key\":\"{entry.key}\",\"text\":\"{entry.text}\"}}"))
            + "]}";
        File.WriteAllText(Path.Combine(tempDirectory, fileName), contents);
        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(tempDirectory);
    }

    private sealed class DummyTinkeringMinePopupTarget
    {
        public string PopupMessageToShow { get; set; } = string.Empty;

        public void HandleEvent(DummyInventoryActionEvent e)
        {
            _ = e;
            _ = DummyPopupShow.ShowYesNo(PopupMessageToShow);
        }
    }

    private sealed class DummyInventoryActionEvent
    {
    }
}
