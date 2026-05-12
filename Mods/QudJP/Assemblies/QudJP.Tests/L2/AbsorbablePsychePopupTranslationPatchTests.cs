using System.Reflection;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class AbsorbablePsychePopupTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        AbsorbablePsychePopupTranslationPatch.ResetForTests();
        DummyPopupShow.Reset();
        DummyAbsorbablePsycheTarget.PopupMessageToShow = string.Empty;
        DummyAbsorbablePsycheTarget.PopupMethod = nameof(DummyPopupShow.Show);
        DynamicTextObservability.ResetForTests();
    }

    [TearDown]
    public void TearDown()
    {
        AbsorbablePsychePopupTranslationPatch.ResetForTests();
        DynamicTextObservability.ResetForTests();
    }

    [Test]
    public void HandleEvent_TranslatesConfirmationPopup_WhenOwnerPatched()
    {
        DummyAbsorbablePsycheTarget.PopupMethod = nameof(DummyPopupShow.ShowYesNo);

        AssertPopupMessage(
            "At the moment of victory, your swelling ego curves the psychic aether and causes the psyche of {{Y|Esper Hunter}} to collide with your own. As the weaker of the two, its binding energy is exceeded and it explodes. Would you like to encode its psionic bits on the holographic boundary of your own psyche?\n\n(+1 Ego permanently)",
            "勝利の瞬間、膨張する自我が精神のエーテルをゆがませ、{{Y|Esper Hunter}}の精神をあなた自身の精神に衝突させる。弱い方であるその精神は束縛エネルギーを超えて爆発する。そのサイオニック片をあなた自身の精神を囲むホログラフィック境界に刻みつけますか？\n\n（恒久的に自我 +1）");

        Assert.That(AbsorbablePsycheHitCount(), Is.EqualTo(1));
    }

    [Test]
    public void HandleEvent_TranslatesEncodePopup_WhenOwnerPatched()
    {
        AssertPopupMessage(
            "You encode the psyche of Esper Hunter and gain +{{C|1}} {{Y|Ego}}!",
            "Esper Hunterの精神を刻みつけ、自我が+{{C|1}}上昇した！");

        Assert.That(AbsorbablePsycheHitCount(), Is.EqualTo(1));
    }

    [Test]
    public void HandleEvent_TranslatesRadiatePopup_WhenOwnerPatched()
    {
        AssertPopupMessage(
            "You pause as the psyche of Esper Hunter radiates into nothingness.",
            "Esper Hunterの精神が無へと放射されていくのを見届ける。");

        Assert.That(AbsorbablePsycheHitCount(), Is.EqualTo(1));
    }

    [Test]
    public void HandleEvent_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent()
    {
        const string source = "You encode the psyche of Esper Hunter and gain +{{C|1}} {{Y|Ego}}!";
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShow(harmony);

            DummyPopupShow.Show(source);

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(source));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void HandleEvent_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        const string source = "You encode the psyche of Esper Hunter and gain +{{C|1}} {{Y|Ego}}!";

        AssertPopupMessage(
            MessageFrameTranslator.MarkDirectTranslation(source),
            source);

        Assert.That(AbsorbablePsycheHitCount(), Is.Zero);
    }

    [Test]
    public void HandleEvent_LeavesEmptyPopupUnchanged_WhenOwnerPatched()
    {
        AssertPopupMessage(string.Empty, string.Empty);
    }

    private static void AssertPopupMessage(string source, string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShow(harmony);
            PatchPopupShowYesNo(harmony);
            PatchOwner(harmony);

            DummyAbsorbablePsycheTarget.PopupMessageToShow = source;
            DummyAbsorbablePsycheTarget.HandleEvent();

            if (string.Equals(DummyAbsorbablePsycheTarget.PopupMethod, nameof(DummyPopupShow.ShowYesNo), StringComparison.Ordinal))
            {
                Assert.That(DummyPopupShow.LastShowYesNoMessage, Is.EqualTo(expected));
            }
            else
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
            }
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void PatchPopupShow(Harmony harmony)
    {
        harmony.Patch(
            original: RequireMethod(
                typeof(DummyPopupShow),
                nameof(DummyPopupShow.Show),
                typeof(string),
                typeof(string),
                typeof(string),
                typeof(bool),
                typeof(bool),
                typeof(bool),
                typeof(bool)),
            prefix: new HarmonyMethod(RequireMethod(typeof(PopupShowTranslationPatch), nameof(PopupShowTranslationPatch.Prefix))));
    }

    private static void PatchPopupShowYesNo(Harmony harmony)
    {
        harmony.Patch(
            original: RequireMethod(
                typeof(DummyPopupShow),
                nameof(DummyPopupShow.ShowYesNo),
                typeof(string),
                typeof(string),
                typeof(bool),
                typeof(int)),
            prefix: new HarmonyMethod(RequireMethod(typeof(PopupShowTranslationPatch), nameof(PopupShowTranslationPatch.Prefix))));
    }

    private static void PatchOwner(Harmony harmony)
    {
        harmony.Patch(
            original: RequireMethod(typeof(DummyAbsorbablePsycheTarget), nameof(DummyAbsorbablePsycheTarget.HandleEvent)),
            prefix: new HarmonyMethod(RequireMethod(
                typeof(AbsorbablePsychePopupTranslationPatch),
                nameof(AbsorbablePsychePopupTranslationPatch.Prefix))),
            finalizer: new HarmonyMethod(RequireMethod(
                typeof(AbsorbablePsychePopupTranslationPatch),
                nameof(AbsorbablePsychePopupTranslationPatch.Finalizer),
                typeof(Exception))));
    }

    private static MethodInfo RequireMethod(Type type, string name, params Type[] parameters)
    {
        if (parameters.Length == 0)
        {
            var methodByName = type.GetMethod(
                name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
            Assert.That(methodByName, Is.Not.Null, $"{type.FullName}.{name} not found");
            return methodByName!;
        }

        var method = type.GetMethod(
            name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance,
            binder: null,
            types: parameters,
            modifiers: null);
        Assert.That(method, Is.Not.Null, $"{type.FullName}.{name} not found");
        return method!;
    }

    private static int AbsorbablePsycheHitCount()
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            nameof(PopupShowTranslationPatch),
            "Popup.Show." + nameof(AbsorbablePsychePopupTranslationPatch));
    }

    private static string CreateHarmonyId() => $"qudjp.tests.{Guid.NewGuid():N}";
}
