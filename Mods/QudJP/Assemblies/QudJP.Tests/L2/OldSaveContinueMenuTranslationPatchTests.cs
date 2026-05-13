using System.Reflection;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class OldSaveContinueMenuTranslationPatchTests
{
    private const string OldSaveSource =
        "That save file looks like it's from an older save format revision (2.0.3). Sorry!\n\nYou can probably change to a previous branch in your game client and get it to load if you want to finish it off.";

    private const string OldSaveExpected =
        "このセーブデータは古いフォーマット（2.0.3）のようです。\nゲームクライアントで以前のブランチに切り替えれば読み込める可能性があります。";

    [SetUp]
    public void SetUp()
    {
        LocalizationAssetResolver.SetLocalizationRootForTests(GetLocalizationRoot());
        Translator.ResetForTests();
        RuntimeDiagnostics.SetVerboseProbesEnabledForTests(true);
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();
        DummyPopupShow.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        RuntimeDiagnostics.SetVerboseProbesEnabledForTests(null);
        Translator.ResetForTests();
        LocalizationAssetResolver.SetLocalizationRootForTests(null);
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();
    }

    [TestCase(nameof(DummyOldSaveContinueMenuTarget.MainMenuContinueMenu))]
    [TestCase(nameof(DummyOldSaveContinueMenuTarget.SaveManagementContinueMenu))]
    public void Patch_TranslatesOldSavePopup_WhenOwnerPatched(string methodName)
    {
        AssertOldSavePopup(methodName, OldSaveSource, OldSaveExpected, expectedOwnerRouteHits: 1);
    }

    [Test]
    public void TryTranslatePopupMessage_DoesNotClaimOldSavePopup_WhenOwnerAbsent()
    {
        var ok = OldSaveContinueMenuTranslationPatch.TryTranslatePopupMessage(
            OldSaveSource,
            nameof(PopupShowTranslationPatch),
            "Popup.Show",
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.False);
            Assert.That(translated, Is.EqualTo(OldSaveSource));
            Assert.That(OwnerRouteHitCount(), Is.Zero);
        });
    }

    [Test]
    public void Patch_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        AssertOldSavePopup(
            nameof(DummyOldSaveContinueMenuTarget.MainMenuContinueMenu),
            MessageFrameTranslator.MarkDirectTranslation(OldSaveSource),
            OldSaveSource,
            expectedOwnerRouteHits: 0);
    }

    [Test]
    public void Patch_RestoresOuterOwnerScopeAfterNestedOwnerPopup()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShowAsync(harmony);
            PatchOwner(harmony, RequireMethod(typeof(NestedOldSaveContinueMenuTarget), nameof(NestedOldSaveContinueMenuTarget.ContinueMenu)));

            var innerTarget = new NestedOldSaveContinueMenuTarget
            {
                PopupMessageToSend = OldSaveSource,
            };
            var outerTarget = new NestedOldSaveContinueMenuTarget
            {
                PopupMessageToSend = OldSaveSource,
                BeforePopup = () =>
                {
                    innerTarget.ContinueMenu();
                    Assert.Multiple(() =>
                    {
                        Assert.That(DummyPopupShow.LastShowAsyncMessage, Is.EqualTo(OldSaveExpected));
                        Assert.That(OwnerRouteHitCount(), Is.EqualTo(1));
                    });
                },
            };

            outerTarget.ContinueMenu();

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowAsyncMessage, Is.EqualTo(OldSaveExpected));
                Assert.That(OwnerRouteHitCount(), Is.EqualTo(2));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Patch_LeavesEmptyPopupUnchanged_WhenOwnerPatched()
    {
        AssertOldSavePopup(
            nameof(DummyOldSaveContinueMenuTarget.MainMenuContinueMenu),
            string.Empty,
            string.Empty,
            expectedOwnerRouteHits: 0);
    }

    private static void AssertOldSavePopup(
        string methodName,
        string source,
        string expected,
        int expectedOwnerRouteHits)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShowAsync(harmony);
            PatchOwner(harmony, RequireOwnerMethod(methodName));

            var target = new DummyOldSaveContinueMenuTarget
            {
                PopupMessageToSend = source,
            };

            _ = RequireOwnerMethod(methodName).Invoke(target, Array.Empty<object>());

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowAsyncMessage, Is.EqualTo(expected));
                Assert.That(OwnerRouteHitCount(), Is.EqualTo(expectedOwnerRouteHits));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void PatchPopupShowAsync(Harmony harmony)
    {
        harmony.Patch(
            original: RequireMethod(typeof(DummyPopupShow), nameof(DummyPopupShow.ShowAsync)),
            prefix: new HarmonyMethod(RequireMethod(typeof(PopupShowTranslationPatch), nameof(PopupShowTranslationPatch.Prefix))));
    }

    private static void PatchOwner(Harmony harmony, MethodInfo original)
    {
        harmony.Patch(
            original: original,
            prefix: new HarmonyMethod(RequireMethod(typeof(OldSaveContinueMenuTranslationPatch), nameof(OldSaveContinueMenuTranslationPatch.Prefix))),
            finalizer: new HarmonyMethod(RequireMethod(typeof(OldSaveContinueMenuTranslationPatch), nameof(OldSaveContinueMenuTranslationPatch.Finalizer), typeof(Exception))));
    }

    private static MethodInfo RequireOwnerMethod(string methodName)
    {
        return RequireMethod(typeof(DummyOldSaveContinueMenuTarget), methodName);
    }

    private static MethodInfo RequireMethod(Type type, string methodName, params Type[] parameterTypes)
    {
        if (parameterTypes.Length == 0)
        {
            return type.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)
                   ?? throw new InvalidOperationException($"Method not found: {type.FullName}.{methodName}");
        }

        return AccessTools.Method(type, methodName, parameterTypes)
               ?? throw new InvalidOperationException($"Method not found: {type.FullName}.{methodName}");
    }

    private static int OwnerRouteHitCount()
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            nameof(PopupShowTranslationPatch),
            "Popup.Show." + nameof(OldSaveContinueMenuTranslationPatch));
    }

    private static string CreateHarmonyId()
    {
        return $"qudjp.tests.{Guid.NewGuid():N}";
    }

    private static string GetLocalizationRoot()
    {
        return Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "../../../../../Localization"));
    }

    private sealed class NestedOldSaveContinueMenuTarget
    {
        public string PopupMessageToSend { get; set; } = string.Empty;

        public Action? BeforePopup { get; set; }

        public void ContinueMenu()
        {
            BeforePopup?.Invoke();
            _ = DummyPopupShow.ShowAsync(PopupMessageToSend);
        }
    }
}
