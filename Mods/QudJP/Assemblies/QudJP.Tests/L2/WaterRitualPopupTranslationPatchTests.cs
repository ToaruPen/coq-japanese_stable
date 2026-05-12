using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class WaterRitualPopupTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        LocalizationAssetResolver.SetLocalizationRootForTests(null);
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

    [TestCase(
        nameof(DummyWaterRitualPopupProducerTarget.WaterRitualBeginHandleEvent),
        nameof(DummyPopupShow.ShowYesNoCancel),
        "Do you want to play a game of Sifrah to perform the formal water ritual with {{G|Tam}}? The formal ritual can be much more impactful. If you do not play the game of Sifrah, the informal water ritual will consume 1 dram of {{B|fresh water}}.",
        "{{G|Tam}}と正式な水の儀式を行うためにシフラーのゲームをプレイしますか？正式な儀式はより大きな影響をもたらすことがあります。シフラーをプレイしない場合、非正式な水の儀式は{{B|fresh water}}を1ドラム消費します。",
        "FormalRitualPrompt")]
    [TestCase(
        nameof(DummyWaterRitualPopupProducerTarget.WaterRitualBeginHandleEvent),
        nameof(DummyPopupShow.ShowFail),
        "You don't have enough {{B|fresh water}} to begin the ritual.",
        "儀式を始めるには{{B|fresh water}}が足りない。",
        "NotEnoughLiquid")]
    [TestCase(
        nameof(DummyWaterRitualPopupProducerTarget.WaterRitualSkillPointHandleEvent),
        nameof(DummyPopupShow.Show),
        "Talking to {{Y|the warden}} rouses in you an inert truth. You once wore the frock of a child. You poured salt through the cracks of your fingers, and you watched worlds form. Can it be all so simple still?",
        "{{Y|the warden}}との会話が、あなたの内に眠る真実を呼び覚ました。あなたはかつて子供の上着をまとっていた。指の隙間から塩を注ぎ、世界が形作られるのを見ていた。今もなお、それほど単純でありうるのだろうか？",
        "SkillPointIntro")]
    [TestCase(
        nameof(DummyWaterRitualPopupProducerTarget.WaterRitualSkillPointHandleEvent),
        nameof(DummyPopupShow.Show),
        "You gained {{C|50}} skill points!",
        "{{C|50}}スキルポイントを得た！",
        "SkillPointGain")]
    [TestCase(
        nameof(DummyWaterRitualPopupProducerTarget.WaterRitualTinkeringRecipeHandleEvent),
        nameof(DummyPopupShow.Show),
        "{{G|Hortensa}} teaches you to craft the item modification {{W|sturdy}}.",
        "{{G|Hortensa}}がアイテム改造{{W|sturdy}}の作り方を教えてくれた。",
        "TinkeringMod")]
    [TestCase(
        nameof(DummyWaterRitualPopupProducerTarget.WaterRitualTinkeringRecipeHandleEvent),
        nameof(DummyPopupShow.Show),
        "{{G|Hortensa}} teaches you to craft {{W|spring-loaded boots}}.",
        "{{G|Hortensa}}が{{W|spring-loaded boots}}の作り方を教えてくれた。",
        "TinkeringRecipe")]
    public void Patch_TranslatesWaterRitualOwnerPopups_WhenOwnerPatched(
        string methodName,
        string popupMethod,
        string source,
        string expected,
        string detail)
    {
        WithPatchedOwnerAndPopup(methodName, popupMethod, () =>
        {
            var target = new DummyWaterRitualPopupProducerTarget
            {
                PopupMethod = popupMethod,
                PopupMessageToShow = source,
            };

            InvokeOwnerMethod(target, methodName);

            Assert.Multiple(() =>
            {
                Assert.That(LastPopupMessage(popupMethod), Is.EqualTo(expected));
                Assert.That(HitCount(detail), Is.EqualTo(1));
            });
        });
    }

    [TestCase(
        nameof(DummyWaterRitualPopupProducerTarget.WaterRitualBeginHandleEvent),
        nameof(DummyPopupShow.ShowYesNoCancel),
        "Do you want to play a game of Sifrah to perform the formal water ritual with {{G|Tam}}? The formal ritual can be much more impactful. If you do not play the game of Sifrah, the informal water ritual will consume 1 dram of {{B|fresh water}}.",
        "FormalRitualPrompt")]
    [TestCase(
        nameof(DummyWaterRitualPopupProducerTarget.WaterRitualSkillPointHandleEvent),
        nameof(DummyPopupShow.Show),
        "You gained {{C|50}} skill points!",
        "SkillPointGain")]
    [TestCase(
        nameof(DummyWaterRitualPopupProducerTarget.WaterRitualTinkeringRecipeHandleEvent),
        nameof(DummyPopupShow.Show),
        "{{G|Hortensa}} teaches you to craft {{W|spring-loaded boots}}.",
        "TinkeringRecipe")]
    public void Patch_DoesNotTranslateWaterRitualPopup_WhenOwnerAbsent(
        string methodName,
        string popupMethod,
        string source,
        string detail)
    {
        _ = methodName;
        WithPatchedPopupOnly(popupMethod, () =>
        {
            InvokePopup(popupMethod, source);

            Assert.Multiple(() =>
            {
                Assert.That(LastPopupMessage(popupMethod), Is.EqualTo(source));
                Assert.That(HitCount(detail), Is.Zero);
            });
        });
    }

    [Test]
    public void Patch_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        const string popupMethod = nameof(DummyPopupShow.Show);
        const string unmarked = "You gained {{C|50}} skill points!";
        var source = MessageFrameTranslator.MarkDirectTranslation(unmarked);

        WithPatchedOwnerAndPopup(
            nameof(DummyWaterRitualPopupProducerTarget.WaterRitualSkillPointHandleEvent),
            popupMethod,
            () =>
            {
                var target = new DummyWaterRitualPopupProducerTarget
                {
                    PopupMethod = popupMethod,
                    PopupMessageToShow = source,
                };

                target.WaterRitualSkillPointHandleEvent();

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(unmarked));
                    Assert.That(HitCount("SkillPointGain"), Is.Zero);
                });
            });
    }

    [Test]
    public void Patch_LeavesUnknownEnglishPopupUnchanged_WhenOwnerPatched()
    {
        const string popupMethod = nameof(DummyPopupShow.Show);
        const string source = "{{G|Tam}} shares an unknown water ritual secret.";

        WithPatchedOwnerAndPopup(
            nameof(DummyWaterRitualPopupProducerTarget.WaterRitualSkillPointHandleEvent),
            popupMethod,
            () =>
            {
                var target = new DummyWaterRitualPopupProducerTarget
                {
                    PopupMethod = popupMethod,
                    PopupMessageToShow = source,
                };

                target.WaterRitualSkillPointHandleEvent();

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(source));
                    Assert.That(HitCount("SkillPointIntro"), Is.Zero);
                    Assert.That(HitCount("SkillPointGain"), Is.Zero);
                });
            });
    }

    [Test]
    public void Patch_LeavesEmptyPopupUnchanged_WhenOwnerPatched()
    {
        const string popupMethod = nameof(DummyPopupShow.Show);

        WithPatchedOwnerAndPopup(
            nameof(DummyWaterRitualPopupProducerTarget.WaterRitualSkillPointHandleEvent),
            popupMethod,
            () =>
            {
                var target = new DummyWaterRitualPopupProducerTarget
                {
                    PopupMethod = popupMethod,
                    PopupMessageToShow = string.Empty,
                };

                target.WaterRitualSkillPointHandleEvent();

                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(string.Empty));
            });
    }

    private static void WithPatchedOwnerAndPopup(string methodName, string popupMethod, Action action)
    {
        var harmonyId = $"qudjp.tests.{Guid.NewGuid():N}";
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopup(harmony, popupMethod);
            harmony.Patch(
                original: RequireOwnerMethod(methodName),
                prefix: new HarmonyMethod(RequireMethod(typeof(WaterRitualPopupTranslationPatch), nameof(WaterRitualPopupTranslationPatch.Prefix))),
                finalizer: new HarmonyMethod(RequireMethod(typeof(WaterRitualPopupTranslationPatch), nameof(WaterRitualPopupTranslationPatch.Finalizer))));

            action();
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void WithPatchedPopupOnly(string popupMethod, Action action)
    {
        var harmonyId = $"qudjp.tests.{Guid.NewGuid():N}";
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopup(harmony, popupMethod);
            action();
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void PatchPopup(Harmony harmony, string popupMethod)
    {
        harmony.Patch(
            original: RequireMethod(typeof(DummyPopupShow), popupMethod),
            prefix: new HarmonyMethod(RequireMethod(typeof(PopupShowTranslationPatch), nameof(PopupShowTranslationPatch.Prefix))));
    }

    private static void InvokeOwnerMethod(DummyWaterRitualPopupProducerTarget target, string methodName)
    {
        _ = RequireOwnerMethod(methodName).Invoke(target, null);
    }

    private static void InvokePopup(string popupMethod, string source)
    {
        if (popupMethod == nameof(DummyPopupShow.ShowYesNoCancel))
        {
            _ = DummyPopupShow.ShowYesNoCancel(source);
            return;
        }

        if (popupMethod == nameof(DummyPopupShow.ShowFail))
        {
            DummyPopupShow.ShowFail(source);
            return;
        }

        DummyPopupShow.Show(source);
    }

    private static string? LastPopupMessage(string popupMethod)
    {
        return popupMethod == nameof(DummyPopupShow.ShowYesNoCancel)
            ? DummyPopupShow.LastShowYesNoCancelMessage
            : DummyPopupShow.LastShowMessage;
    }

    private static int HitCount(string detail)
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            nameof(PopupShowTranslationPatch),
            "Popup.ProducerText." + nameof(WaterRitualPopupTranslationPatch) + "." + detail);
    }

    private static MethodInfo RequireOwnerMethod(string methodName)
    {
        return RequireMethod(typeof(DummyWaterRitualPopupProducerTarget), methodName);
    }

    private static MethodInfo RequireMethod(Type type, string methodName)
    {
        return type.GetMethod(
                   methodName,
                   BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)
               ?? throw new InvalidOperationException($"Method not found: {type.FullName}.{methodName}");
    }

    private sealed class DummyWaterRitualPopupProducerTarget
    {
        public string PopupMessageToShow { get; set; } = string.Empty;

        public string PopupMethod { get; set; } = nameof(DummyPopupShow.Show);

        [MethodImpl(MethodImplOptions.NoInlining)]
        public bool WaterRitualBeginHandleEvent()
        {
            EmitPopup(nameof(WaterRitualBeginHandleEvent));
            return true;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public bool WaterRitualSkillPointHandleEvent()
        {
            EmitPopup(nameof(WaterRitualSkillPointHandleEvent));
            return true;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public bool WaterRitualTinkeringRecipeHandleEvent()
        {
            EmitPopup(nameof(WaterRitualTinkeringRecipeHandleEvent));
            return true;
        }

        private void EmitPopup(string route)
        {
            _ = route;
            WaterRitualPopupTranslationPatchTests.InvokePopup(PopupMethod, PopupMessageToShow);
        }
    }
}
