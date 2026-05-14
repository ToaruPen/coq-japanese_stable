using System.Reflection;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class StatusScreenPopupTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        RuntimeDiagnostics.SetVerboseProbesEnabledForTests(true);
        DynamicTextObservability.ResetForTests();
        LocalizationAssetResolver.SetLocalizationRootForTests(GetLocalizationRoot());
        StatusScreenPopupTranslationPatch.ResetForTests();
        DummyPopupShow.Reset();
        DummyStatusScreenPopupTarget.MessageToSend = string.Empty;
    }

    [TearDown]
    public void TearDown()
    {
        StatusScreenPopupTranslationPatch.ResetForTests();
        LocalizationAssetResolver.SetLocalizationRootForTests(null);
        RuntimeDiagnostics.SetVerboseProbesEnabledForTests(null);
        DynamicTextObservability.ResetForTests();
    }

    [TestCase(
        "Your Strength is {{C|16}}.\n\nIt will cost {{C|1}} attribute point to increase Strength by 1.\nDo you wish to increase this attribute?",
        "筋力は{{C|16}}。\n\n筋力を1上げるには属性ポイントが{{C|1}}ポイント必要だ。\nこの属性を上げますか？")]
    [TestCase(
        "Your base Agility is {{C|15}}, modified to {{G|17}}.\n\nYou may not raise an attribute above 100.",
        "敏捷の基本値は{{C|15}}で、{{G|17}}に修正されている。\n\n属性を100より高く上げることはできない。")]
    [TestCase(
        "Your base Toughness is {{C|14}}, modified to {{R|12}}.\n\nYou have no attribute points to raise this attribute.",
        "頑健の基本値は{{C|14}}で、{{R|12}}に修正されている。\n\nこの属性を上げるための属性ポイントがない。")]
    [TestCase(
        "You have increased your Ego to {{C|18}}!",
        "自我を{{C|18}}に上げた！")]
    public void BuyStat_TranslatesAttributePurchasePopups_WhenOwnerPatched(string source, string expected)
    {
        AssertPopupMessage(
            ownerMethod: RequireMethod(typeof(DummyStatusScreenPopupTarget), nameof(DummyStatusScreenPopupTarget.BuyStat), typeof(DummyGameObject), typeof(string)),
            source,
            expected);
    }

    [Test]
    public void BuyStat_LeavesUnknownAttributeTailUnchanged_WhenOwnerPatched()
    {
        const string source = "Your Strength is {{C|16}}.\n\n{{W|Unrecognized tail}}";
        AssertPopupMessage(
            ownerMethod: RequireMethod(typeof(DummyStatusScreenPopupTarget), nameof(DummyStatusScreenPopupTarget.BuyStat), typeof(DummyGameObject), typeof(string)),
            source,
            source);
    }

    [TestCase(
        "You gain {{C|Light Manipulation}}!",
        "{{C|光操作}}を得た！")]
    [TestCase(
        "You have all available mutations.",
        "利用可能な変異はすべて持っている。")]
    public void BuyRandomMutation_TranslatesMutationChoicePopups_WhenOwnerPatched(string source, string expected)
    {
        AssertPopupMessage(
            ownerMethod: RequireMethod(typeof(DummyStatusScreenPopupTarget), nameof(DummyStatusScreenPopupTarget.BuyRandomMutation), typeof(DummyGameObject)),
            source,
            expected);
    }

    [Test]
    public void Show_TranslatesPsychicGlimmerDebugPopup_WhenOwnerPatched()
    {
        AssertPopupMessage(
            ownerMethod: RequireMethod(typeof(DummyStatusScreenPopupTarget), nameof(DummyStatusScreenPopupTarget.Show), typeof(DummyGameObject)),
            "TODOJASON GLIMMER={{C|42}}",
            "TODOJASON サイキック・グリマー={{C|42}}");

        Assert.That(StatusScreenPopupHitCount(), Is.EqualTo(1));
    }

    [Test]
    public void Show_DoesNotClaimRuntimePsychicGlimmerDescription_WhenOwnerPatched()
    {
        const string source =
            "{{K|What you understood to be the psychic sea was only a pond. There are other watchers now, countless in number, beyond the gulf of materiality. Points of light glimmer in all directions, but what are directions on a space that cannot be ordered? All you know now is of an aether vaster than the very mathematics that describe it. And you are not nor will you ever be again alone.}}";
        const string expectedFallback =
            "{{K|あなたが理解していたものは、広大な海ではなくただの池だった。今や見張る者はさらにいる。物質の彼方に無数にいるのだ。光の点が四方八方で瞬くが、秩序づけられない空間における方角とは何だろう？ いま知るのは、それを記述する数学ですら及ばないほど広大なエーテルのことだけだ。そしてあなたは、もう二度と独りではない。}}";

        var translated = TranslatePopupMessage(
            ownerMethod: RequireMethod(typeof(DummyStatusScreenPopupTarget), nameof(DummyStatusScreenPopupTarget.Show), typeof(DummyGameObject)),
            source);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.EqualTo(expectedFallback));
            Assert.That(StatusScreenPopupHitCount(), Is.Zero);
        });
    }

    [Test]
    public void TryTranslatePopupMessage_TranslatesGainedMutation_WhenOwnerScopeIsActive()
    {
        StatusScreenPopupTranslationPatch.Prefix();
        try
        {
            var ok = StatusScreenPopupTranslationPatch.TryTranslatePopupMessage(
                "You gain {{C|Light Manipulation}}!",
                nameof(PopupShowTranslationPatch),
                "Popup.Show",
                out var translated);

            Assert.Multiple(() =>
            {
                Assert.That(ok, Is.True);
                Assert.That(translated, Is.EqualTo("{{C|光操作}}を得た！"));
            });
        }
        finally
        {
            _ = StatusScreenPopupTranslationPatch.Finalizer(null);
        }
    }

    [Test]
    public void StatusScreenPopup_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShow(harmony);

            DummyPopupShow.Show("You gain {{C|Light Manipulation}}!");

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("You gain {{C|Light Manipulation}}!"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void StatusScreenPopup_DoesNotTranslatePsychicGlimmerPopup_WhenOwnerAbsent()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShow(harmony);

            DummyPopupShow.Show("TODOJASON GLIMMER={{C|42}}");

            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("TODOJASON GLIMMER={{C|42}}"));
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void StatusScreenPopup_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        AssertPopupMessage(
            ownerMethod: RequireMethod(typeof(DummyStatusScreenPopupTarget), nameof(DummyStatusScreenPopupTarget.BuyStat), typeof(DummyGameObject), typeof(string)),
            MessageFrameTranslator.MarkDirectTranslation("You have increased your Ego to {{C|18}}!"),
            "You have increased your Ego to {{C|18}}!");
    }

    [Test]
    public void StatusScreenPopup_LeavesEmptyPopupUnchanged_WhenOwnerPatched()
    {
        AssertPopupMessage(
            ownerMethod: RequireMethod(typeof(DummyStatusScreenPopupTarget), nameof(DummyStatusScreenPopupTarget.BuyRandomMutation), typeof(DummyGameObject)),
            string.Empty,
            string.Empty);
    }

    private static void AssertPopupMessage(MethodInfo ownerMethod, string source, string expected)
    {
        Assert.That(TranslatePopupMessage(ownerMethod, source), Is.EqualTo(expected));
    }

    private static string TranslatePopupMessage(MethodInfo ownerMethod, string source)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShow(harmony);
            PatchOwner(harmony, ownerMethod);

            DummyStatusScreenPopupTarget.MessageToSend = source;
            _ = ownerMethod.Invoke(null, CreateOwnerArguments(ownerMethod));

            return DummyPopupShow.LastShowMessage ?? string.Empty;
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static object[] CreateOwnerArguments(MethodInfo ownerMethod)
    {
        return ownerMethod.Name switch
        {
            nameof(DummyStatusScreenPopupTarget.BuyStat) => new object[] { new DummyGameObject(), "Strength" },
            nameof(DummyStatusScreenPopupTarget.BuyRandomMutation) => new object[] { new DummyGameObject() },
            nameof(DummyStatusScreenPopupTarget.Show) => new object[] { new DummyGameObject() },
            _ => Array.Empty<object>(),
        };
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

    private static void PatchOwner(Harmony harmony, MethodInfo original)
    {
        harmony.Patch(
            original: original,
            prefix: new HarmonyMethod(RequireMethod(typeof(StatusScreenPopupTranslationPatch), nameof(StatusScreenPopupTranslationPatch.Prefix))),
            finalizer: new HarmonyMethod(RequireMethod(typeof(StatusScreenPopupTranslationPatch), nameof(StatusScreenPopupTranslationPatch.Finalizer), typeof(Exception))));
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

    private static int StatusScreenPopupHitCount()
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            nameof(PopupShowTranslationPatch),
            "Popup.Show.StatusScreenPopupTranslationPatch");
    }

    private static string GetLocalizationRoot()
    {
        return Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "../../../../../Localization"));
    }
}
