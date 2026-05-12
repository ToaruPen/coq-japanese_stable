using System.Reflection;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class ConversationTakeItemPopupTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        ConversationTakeItemPopupTranslationPatch.ResetForTests();
        DummyPopupShow.Reset();
        DummyConversationTakeItemTarget.PopupMessageToShow = string.Empty;
        DummyConversationTakeItemTarget.PopupMethod = nameof(DummyPopupShow.Show);
        DynamicTextObservability.ResetForTests();
    }

    [TearDown]
    public void TearDown()
    {
        ConversationTakeItemPopupTranslationPatch.ResetForTests();
        DynamicTextObservability.ResetForTests();
    }

    [Test]
    public void Execute_TranslatesCannotGivePopup_WhenOwnerPatched()
    {
        DummyConversationTakeItemTarget.PopupMethod = nameof(DummyPopupShow.ShowFail);

        AssertPopupMessage(
            "You cannot give {{Y|奇妙な小物}}!",
            "{{Y|奇妙な小物}}を渡せない！");

        Assert.That(TakeItemHitCount(), Is.EqualTo(1));
    }

    [Test]
    public void Execute_TranslatesTakeSuccessPopup_WhenOwnerPatched()
    {
        AssertPopupMessage(
            "Q Girl takes {{Y|奇妙な小物}}.",
            "Q Girlは{{Y|奇妙な小物}}を受け取った。");

        Assert.That(TakeItemHitCount(), Is.EqualTo(1));
    }

    [Test]
    public void Execute_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent()
    {
        const string source = "Q Girl takes {{Y|奇妙な小物}}.";
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
    public void Execute_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        const string source = "Q Girl takes {{Y|奇妙な小物}}.";

        AssertPopupMessage(
            MessageFrameTranslator.MarkDirectTranslation(source),
            source);

        Assert.That(TakeItemHitCount(), Is.Zero);
    }

    [Test]
    public void Execute_LeavesEmptyPopupUnchanged_WhenOwnerPatched()
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
            PatchOwner(harmony);

            DummyConversationTakeItemTarget.PopupMessageToShow = source;
            DummyConversationTakeItemTarget.Execute();

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
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

    private static void PatchOwner(Harmony harmony)
    {
        harmony.Patch(
            original: RequireMethod(
                typeof(DummyConversationTakeItemTarget),
                nameof(DummyConversationTakeItemTarget.Execute)),
            prefix: new HarmonyMethod(RequireMethod(
                typeof(ConversationTakeItemPopupTranslationPatch),
                nameof(ConversationTakeItemPopupTranslationPatch.Prefix))),
            finalizer: new HarmonyMethod(RequireMethod(
                typeof(ConversationTakeItemPopupTranslationPatch),
                nameof(ConversationTakeItemPopupTranslationPatch.Finalizer),
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

    private static int TakeItemHitCount()
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            nameof(PopupShowTranslationPatch),
            "Popup.Show." + nameof(ConversationTakeItemPopupTranslationPatch));
    }

    private static string CreateHarmonyId() => $"qudjp.tests.{Guid.NewGuid():N}";
}
