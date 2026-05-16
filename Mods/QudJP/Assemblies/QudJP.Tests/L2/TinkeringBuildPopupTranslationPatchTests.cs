using System.Reflection;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class TinkeringBuildPopupTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        TinkeringBuildPopupTranslationPatch.ResetForTests();
        DummyPopupShow.Reset();
        DummyTinkeringBuildTarget.PopupMessageToShow = string.Empty;
        DummyTinkeringBuildTarget.PopupMethod = nameof(DummyPopupShow.Show);
        DynamicTextObservability.ResetForTests();
    }

    [TearDown]
    public void TearDown()
    {
        TinkeringBuildPopupTranslationPatch.ResetForTests();
        DynamicTextObservability.ResetForTests();
    }

    [Test]
    public void PerformUITinkerBuild_TranslatesMissingIngredientPopup_WhenOwnerPatched()
    {
        DummyTinkeringBuildTarget.PopupMethod = nameof(DummyPopupShow.ShowFail);

        AssertPopupMessage(
            "You don't have the required ingredient: {{Y|銅線}} or {{C|電池}}!",
            "必要な材料が足りない: {{Y|銅線}}または{{C|電池}}！");

        Assert.That(TinkeringBuildHitCount(), Is.EqualTo(1));
    }

    [TestCase("You tinker up {{Y|a freeze grenade}}!", "{{Y|freeze grenade}}を作った！")]
    [TestCase("You tinker up two {{Y|freeze grenades}}!", "{{Y|freeze grenades}}を2個作った！")]
    public void PerformUITinkerBuild_TranslatesSuccessPopups_WhenOwnerPatched(string source, string expected)
    {
        AssertPopupMessage(source, expected);

        Assert.That(TinkeringBuildHitCount(), Is.EqualTo(1));
    }

    [Test]
    public void PerformUITinkerBuild_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent()
    {
        const string source = "You tinker up a freeze grenade!";
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
    public void PerformUITinkerBuild_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        const string source = "You tinker up a freeze grenade!";

        AssertPopupMessage(
            MessageFrameTranslator.MarkDirectTranslation(source),
            source);

        Assert.That(TinkeringBuildHitCount(), Is.Zero);
    }

    [Test]
    public void PerformUITinkerBuild_LeavesUnknownCountPopupUnchanged_WhenOwnerPatched()
    {
        const string source = "You tinker up several {{Y|freeze grenades}}!";

        AssertPopupMessage(source, source);

        Assert.That(TinkeringBuildHitCount(), Is.Zero);
    }

    [Test]
    public void PerformUITinkerBuild_RestoresOuterOwnerScopeAfterNestedOwnerPopup()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShow(harmony);
            PatchOwner(harmony, RequireMethod(typeof(NestedTinkeringBuildTarget), nameof(NestedTinkeringBuildTarget.PerformUITinkerBuild)));

            var innerTarget = new NestedTinkeringBuildTarget
            {
                PopupMessageToShow = "You tinker up {{Y|a freeze grenade}}!",
            };
            var outerTarget = new NestedTinkeringBuildTarget
            {
                PopupMessageToShow = "You tinker up two {{Y|freeze grenades}}!",
                BeforePopup = () =>
                {
                    innerTarget.PerformUITinkerBuild();
                    Assert.Multiple(() =>
                    {
                        Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("{{Y|freeze grenade}}を作った！"));
                        Assert.That(TinkeringBuildHitCount(), Is.EqualTo(1));
                    });
                },
            };

            outerTarget.PerformUITinkerBuild();

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("{{Y|freeze grenades}}を2個作った！"));
                Assert.That(TinkeringBuildHitCount(), Is.EqualTo(2));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void PerformUITinkerBuild_LeavesEmptyPopupUnchanged_WhenOwnerPatched()
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

            DummyTinkeringBuildTarget.PopupMessageToShow = source;
            DummyTinkeringBuildTarget.PerformUITinkerBuild();

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
        PatchOwner(
            harmony,
            RequireMethod(
                typeof(DummyTinkeringBuildTarget),
                nameof(DummyTinkeringBuildTarget.PerformUITinkerBuild)));
    }

    private static void PatchOwner(Harmony harmony, MethodInfo original)
    {
        harmony.Patch(
            original: original,
            prefix: new HarmonyMethod(RequireMethod(
                typeof(TinkeringBuildPopupTranslationPatch),
                nameof(TinkeringBuildPopupTranslationPatch.Prefix))),
            finalizer: new HarmonyMethod(RequireMethod(
                typeof(TinkeringBuildPopupTranslationPatch),
                nameof(TinkeringBuildPopupTranslationPatch.Finalizer),
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

    private static int TinkeringBuildHitCount()
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            nameof(PopupShowTranslationPatch),
            "Popup.Show." + nameof(TinkeringBuildPopupTranslationPatch));
    }

    private static string CreateHarmonyId() => $"qudjp.tests.{Guid.NewGuid():N}";

    private sealed class NestedTinkeringBuildTarget
    {
        public string PopupMessageToShow { get; set; } = string.Empty;

        public Action? BeforePopup { get; set; }

        public void PerformUITinkerBuild()
        {
            BeforePopup?.Invoke();
            DummyPopupShow.Show(PopupMessageToShow);
        }
    }
}
