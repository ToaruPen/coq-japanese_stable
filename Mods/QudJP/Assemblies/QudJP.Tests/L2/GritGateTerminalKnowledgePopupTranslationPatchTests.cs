using System.Reflection;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class GritGateTerminalKnowledgePopupTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        GritGateTerminalKnowledgePopupTranslationPatch.ResetForTests();
        DummyPopupShow.Reset();
        DummyGritGateTerminalKnowledgeTarget.PopupMessageToShow = string.Empty;
        DynamicTextObservability.ResetForTests();
    }

    [TearDown]
    public void TearDown()
    {
        GritGateTerminalKnowledgePopupTranslationPatch.ResetForTests();
        DynamicTextObservability.ResetForTests();
    }

    [Test]
    public void Activate_TranslatesInsightPopup_WhenOwnerPatched()
    {
        const string source = "Ereshkigal delivers insight from the Thin World:\n\nThe location of {{Y|Bethesda Susa}}";
        const string expected = "エレシュキガルは薄界からの洞察を授ける:\n\nThe location of {{Y|Bethesda Susa}}";

        AssertPopupMessage(source, expected);

        Assert.That(
            DynamicTextObservability.GetRouteFamilyHitCountForTests(
                nameof(PopupShowTranslationPatch),
                "Popup.Show." + nameof(GritGateTerminalKnowledgePopupTranslationPatch)),
            Is.EqualTo(1));
    }

    [Test]
    public void Activate_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent()
    {
        const string source = "Ereshkigal delivers insight from the Thin World:\n\nThe location of Bethesda Susa";
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
    public void Activate_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        const string source = "Ereshkigal delivers insight from the Thin World:\n\nThe location of Bethesda Susa";

        AssertPopupMessage(
            MessageFrameTranslator.MarkDirectTranslation(source),
            source);

        Assert.That(
            DynamicTextObservability.GetRouteFamilyHitCountForTests(
                nameof(PopupShowTranslationPatch),
                "Popup.Show." + nameof(GritGateTerminalKnowledgePopupTranslationPatch)),
            Is.Zero);
    }

    [Test]
    public void Activate_LeavesEmptyPopupUnchanged_WhenOwnerPatched()
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

            DummyGritGateTerminalKnowledgeTarget.PopupMessageToShow = source;
            DummyGritGateTerminalKnowledgeTarget.Activate();

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
                typeof(DummyGritGateTerminalKnowledgeTarget),
                nameof(DummyGritGateTerminalKnowledgeTarget.Activate)),
            prefix: new HarmonyMethod(RequireMethod(
                typeof(GritGateTerminalKnowledgePopupTranslationPatch),
                nameof(GritGateTerminalKnowledgePopupTranslationPatch.Prefix))),
            finalizer: new HarmonyMethod(RequireMethod(
                typeof(GritGateTerminalKnowledgePopupTranslationPatch),
                nameof(GritGateTerminalKnowledgePopupTranslationPatch.Finalizer),
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

    private static string CreateHarmonyId() => $"qudjp.tests.{Guid.NewGuid():N}";
}
