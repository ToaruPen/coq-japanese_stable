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

    [Test]
    public void Activate_LeavesUnknownPopupUnchanged_WhenOwnerPatched()
    {
        const string source = "Ereshkigal records a silence in the Thin World.";

        AssertPopupMessage(source, source);

        Assert.That(
            DynamicTextObservability.GetRouteFamilyHitCountForTests(
                nameof(PopupShowTranslationPatch),
                "Popup.Show." + nameof(GritGateTerminalKnowledgePopupTranslationPatch)),
            Is.Zero);
    }

    [Test]
    public void Activate_RestoresOuterOwnerScopeAfterNestedOwnerPopup()
    {
        const string innerSource = "Ereshkigal delivers insight from the Thin World:\n\nThe location of {{Y|Grit Gate}}";
        const string innerExpected = "エレシュキガルは薄界からの洞察を授ける:\n\nThe location of {{Y|Grit Gate}}";
        const string outerSource = "Ereshkigal delivers insight from the Thin World:\n\nThe location of {{Y|Bethesda Susa}}";
        const string outerExpected = "エレシュキガルは薄界からの洞察を授ける:\n\nThe location of {{Y|Bethesda Susa}}";
        var target = new DummyNestedGritGateTerminalKnowledgeTarget
        {
            InnerPopupMessageToShow = innerSource,
            OuterPopupMessageToShow = outerSource,
        };
        target.BeforeOuterPopup = () =>
        {
            target.InnerActivate();

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(innerExpected));
                Assert.That(GetHitCount(), Is.EqualTo(1));
            });
        };

        WithPatchedOwners(
            [
                RequireMethod(typeof(DummyNestedGritGateTerminalKnowledgeTarget), nameof(DummyNestedGritGateTerminalKnowledgeTarget.OuterActivate)),
                RequireMethod(typeof(DummyNestedGritGateTerminalKnowledgeTarget), nameof(DummyNestedGritGateTerminalKnowledgeTarget.InnerActivate)),
            ],
            target.OuterActivate);

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(outerExpected));
            Assert.That(GetHitCount(), Is.EqualTo(2));
        });
    }

    private static void AssertPopupMessage(string source, string expected)
    {
        WithPatchedOwners(
            [RequireMethod(typeof(DummyGritGateTerminalKnowledgeTarget), nameof(DummyGritGateTerminalKnowledgeTarget.Activate))],
            () =>
            {
                DummyGritGateTerminalKnowledgeTarget.PopupMessageToShow = source;
                DummyGritGateTerminalKnowledgeTarget.Activate();

                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
            });
    }

    private static void WithPatchedOwners(MethodInfo[] ownerMethods, Action action)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShow(harmony);
            foreach (var ownerMethod in ownerMethods)
            {
                PatchOwner(harmony, ownerMethod);
            }

            action();
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

    private static void PatchOwner(Harmony harmony, MethodInfo ownerMethod)
    {
        harmony.Patch(
            original: ownerMethod,
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

    private static int GetHitCount()
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            nameof(PopupShowTranslationPatch),
            "Popup.Show." + nameof(GritGateTerminalKnowledgePopupTranslationPatch));
    }

    private sealed class DummyNestedGritGateTerminalKnowledgeTarget
    {
        public string OuterPopupMessageToShow { get; init; } = string.Empty;
        public string InnerPopupMessageToShow { get; init; } = string.Empty;
        public Action? BeforeOuterPopup { get; set; }

        public void OuterActivate()
        {
            BeforeOuterPopup?.Invoke();
            DummyPopupShow.Show(OuterPopupMessageToShow);
        }

        public void InnerActivate()
        {
            DummyPopupShow.Show(InnerPopupMessageToShow);
        }
    }
}
