using System.Reflection;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class PickItemTakeAllPopupTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        PickItemTakeAllPopupTranslationPatch.ResetForTests();
        DummyPopupShow.Reset();
        DummyPickItemTakeAllTarget.PopupMessageToShow = string.Empty;
        DynamicTextObservability.ResetForTests();
    }

    [TearDown]
    public void TearDown()
    {
        PickItemTakeAllPopupTranslationPatch.ResetForTests();
        DynamicTextObservability.ResetForTests();
    }

    [TestCase(
        "Taking this object will put you over your weight limit. Are you sure you want to do it?",
        "これを取ると重量制限を超えます。本当に実行しますか？")]
    [TestCase(
        "Taking these objects will put you over your weight limit. Are you sure you want to do it?",
        "これらを取ると重量制限を超えます。本当に実行しますか？")]
    [TestCase(
        "Taking all these objects will put you over your weight limit. Are you sure you want to do it?",
        "これらすべてを取ると重量制限を超えます。本当に実行しますか？")]
    public void TakeAll_TranslatesOverweightConfirmationPopup_WhenOwnerPatched(string source, string expected)
    {
        AssertPopupMessage(source, expected);

        Assert.That(
            DynamicTextObservability.GetRouteFamilyHitCountForTests(
                nameof(PopupShowTranslationPatch),
                "Popup.Show." + nameof(PickItemTakeAllPopupTranslationPatch)),
            Is.EqualTo(1));
    }

    [Test]
    public void TakeAll_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent()
    {
        const string source = "Taking this object will put you over your weight limit. Are you sure you want to do it?";
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShowYesNo(harmony);

            DummyPopupShow.ShowYesNo(source);

            Assert.That(DummyPopupShow.LastShowYesNoMessage, Is.EqualTo(source));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void TakeAll_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        const string source = "Taking this object will put you over your weight limit. Are you sure you want to do it?";

        AssertPopupMessage(
            MessageFrameTranslator.MarkDirectTranslation(source),
            source);

        Assert.That(
            DynamicTextObservability.GetRouteFamilyHitCountForTests(
                nameof(PopupShowTranslationPatch),
                "Popup.Show." + nameof(PickItemTakeAllPopupTranslationPatch)),
            Is.Zero);
    }

    [Test]
    public void TakeAll_LeavesUnknownMarkupPopupUnchanged_WhenOwnerPatched()
    {
        const string source = "Taking {{unknown|this object}} will put you over your weight limit. Are you sure you want to do it?";

        AssertPopupMessage(source, source);

        Assert.That(
            DynamicTextObservability.GetRouteFamilyHitCountForTests(
                nameof(PopupShowTranslationPatch),
                "Popup.Show." + nameof(PickItemTakeAllPopupTranslationPatch)),
            Is.Zero);
    }

    [Test]
    public void TakeAll_RestoresOuterOwnerScopeAfterNestedOwnerPopup()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShowYesNo(harmony);
            PatchOwner(harmony, RequireMethod(typeof(NestedPickItemTakeAllTarget), nameof(NestedPickItemTakeAllTarget.TakeAll)));

            var innerTarget = new NestedPickItemTakeAllTarget
            {
                PopupMessageToShow = "Taking this object will put you over your weight limit. Are you sure you want to do it?",
            };
            var outerTarget = new NestedPickItemTakeAllTarget
            {
                PopupMessageToShow = "Taking these objects will put you over your weight limit. Are you sure you want to do it?",
                BeforePopup = () =>
                {
                    innerTarget.TakeAll();
                    Assert.Multiple(() =>
                    {
                        Assert.That(DummyPopupShow.LastShowYesNoMessage, Is.EqualTo("これを取ると重量制限を超えます。本当に実行しますか？"));
                        Assert.That(TakeAllHitCount(), Is.EqualTo(1));
                    });
                },
            };

            outerTarget.TakeAll();

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowYesNoMessage, Is.EqualTo("これらを取ると重量制限を超えます。本当に実行しますか？"));
                Assert.That(TakeAllHitCount(), Is.EqualTo(2));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void TakeAll_LeavesEmptyPopupUnchanged_WhenOwnerPatched()
    {
        AssertPopupMessage(string.Empty, string.Empty);
    }

    private static void AssertPopupMessage(string source, string expected)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShowYesNo(harmony);
            PatchOwner(harmony);

            DummyPickItemTakeAllTarget.PopupMessageToShow = source;
            DummyPickItemTakeAllTarget.TakeAll();

            Assert.That(DummyPopupShow.LastShowYesNoMessage, Is.EqualTo(expected));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
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
        PatchOwner(
            harmony,
            RequireMethod(
                typeof(DummyPickItemTakeAllTarget),
                nameof(DummyPickItemTakeAllTarget.TakeAll)));
    }

    private static void PatchOwner(Harmony harmony, MethodInfo original)
    {
        harmony.Patch(
            original: original,
            prefix: new HarmonyMethod(RequireMethod(
                typeof(PickItemTakeAllPopupTranslationPatch),
                nameof(PickItemTakeAllPopupTranslationPatch.Prefix))),
            finalizer: new HarmonyMethod(RequireMethod(
                typeof(PickItemTakeAllPopupTranslationPatch),
                nameof(PickItemTakeAllPopupTranslationPatch.Finalizer),
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

    private static int TakeAllHitCount()
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            nameof(PopupShowTranslationPatch),
            "Popup.Show." + nameof(PickItemTakeAllPopupTranslationPatch));
    }

    private static string CreateHarmonyId() => $"qudjp.tests.{Guid.NewGuid():N}";

    private sealed class NestedPickItemTakeAllTarget
    {
        public string PopupMessageToShow { get; set; } = string.Empty;

        public Action? BeforePopup { get; set; }

        public void TakeAll()
        {
            BeforePopup?.Invoke();
            _ = DummyPopupShow.ShowYesNo(PopupMessageToShow);
        }
    }
}
