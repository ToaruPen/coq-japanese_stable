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
        StatusScreenMutationPopupTranslationPatch.ResetForTests();
        DummyPopupShow.Reset();
        DummyStatusScreenPopupTarget.MessageToSend = string.Empty;
    }

    [TearDown]
    public void TearDown()
    {
        StatusScreenPopupTranslationPatch.ResetForTests();
        StatusScreenMutationPopupTranslationPatch.ResetForTests();
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
    public void ShowMutationPopup_TranslatesUpgradePrompt_WhenOwnerPatched()
    {
        const string source =
            "You generate a wall of force.\n\n{{w|This rank}}:\n9 contiguous stationary force fields.\n\n{{w|Next rank}}:\n10 contiguous stationary force fields.\n\nIt will cost {{C|1}} mutation point to increase Force Wall's rank by 1.\nDo you wish to increase this mutation's rank?";

        var translated = TranslatePopupMessage(
            ownerMethod: RequireMethod(
                typeof(DummyStatusScreenPopupTarget),
                nameof(DummyStatusScreenPopupTarget.ShowMutationPopup),
                typeof(DummyGameObject),
                typeof(DummyCharacterMutation)),
            source);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Does.StartWith("力の壁を生み出し、9マス連続の力場で敵を遮る。"));
            Assert.That(translated, Does.Contain("{{w|現在ランク}}:"));
            Assert.That(translated, Does.Contain("{{w|次ランク}}:"));
            Assert.That(translated, Does.EndWith("力場壁のランクを1上げるには変異ポイントが{{C|1}}ポイント必要だ。\nこの変異のランクを上げますか？"));
        });
    }

    [Test]
    public void ShowMutationPopup_PreservesRankBoostReasonsBeforeUpgradePrompt_WhenOwnerPatched()
    {
        const string source =
            "You generate a wall of force.\n\n{{w|This rank}}:\n9 contiguous stationary force fields.\n\n{{w|Next rank}}:\n10 contiguous stationary force fields.\n\n{{G|+ This mutation's rank is increased by 1 due to your high adrenaline.}}\n\nIt will cost {{C|1}} mutation point to increase Force Wall's rank by 1.\nDo you wish to increase this mutation's rank?";

        var translated = TranslatePopupMessage(
            ownerMethod: RequireMethod(
                typeof(DummyStatusScreenPopupTarget),
                nameof(DummyStatusScreenPopupTarget.ShowMutationPopup),
                typeof(DummyGameObject),
                typeof(DummyCharacterMutation)),
            source);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Does.Contain("{{w|現在ランク}}:"));
            Assert.That(translated, Does.Contain("{{w|次ランク}}:"));
            Assert.That(translated, Does.Contain("{{G|+ この変異のランクは高いアドレナリンにより1上昇している。}}"));
            Assert.That(translated, Does.EndWith("力場壁のランクを1上げるには変異ポイントが{{C|1}}ポイント必要だ。\nこの変異のランクを上げますか？"));
        });
    }

    [Test]
    public void ShowMutationPopup_TranslatesInsufficientPointsTailPreservingColor_WhenOwnerPatched()
    {
        AssertPopupMessage(
            ownerMethod: RequireMethod(
                typeof(DummyStatusScreenPopupTarget),
                nameof(DummyStatusScreenPopupTarget.ShowMutationPopup),
                typeof(DummyGameObject),
                typeof(DummyCharacterMutation)),
            "{{C|You do not have enough mutation points to increase that mutation's rank.}}",
            "{{C|その変異のランクを上げるための変異ポイントが足りない。}}");
    }

    [Test]
    public void ShowMutationPopup_TranslatesIncreasedRankAndRankBoost_WhenOwnerPatched()
    {
        AssertPopupMessage(
            ownerMethod: RequireMethod(
                typeof(DummyStatusScreenPopupTarget),
                nameof(DummyStatusScreenPopupTarget.ShowMutationPopup),
                typeof(DummyGameObject),
                typeof(DummyCharacterMutation)),
            "You have increased Force Wall's base rank to {{C|2}}!\n\n{{G|* This mutation's base rank is 2.}}",
            "力場壁の基本ランクを{{C|2}}に上げた！\n\n{{G|* この変異の基本ランクは2。}}");
    }

    [TestCase(
        "{{K|* You do not possess this defect inherently, and so you cannot advance its rank.}}",
        "{{K|* この欠陥を本来持っていないため、ランクを上げることはできない。}}")]
    [TestCase(
        "{{G|+ All your mutations' ranks are increased by 2.}}",
        "{{G|+ すべての変異ランクが2上昇している。}}")]
    [TestCase(
        "{{R|- All your defects' ranks are decreased by 1.}}",
        "{{R|- すべての欠陥ランクが1低下している。}}")]
    [TestCase(
        "{{G|+ All your Physical mutations' ranks are increased by 2.}}",
        "{{G|+ すべての身体的変異ランクが2上昇している。}}")]
    [TestCase(
        "{{R|- All your Mental defects' ranks are decreased by 1.}}",
        "{{R|- すべての精神的欠陥ランクが1低下している。}}")]
    [TestCase(
        "{{G|+ This mutation's rank is increased by 2 due to your high adrenaline.}}",
        "{{G|+ この変異のランクは高いアドレナリンにより2上昇している。}}")]
    [TestCase(
        "{{G|+ This mutation's rank is increased by 3 due to being rapidly advanced 1 time.}}",
        "{{G|+ この変異のランクは1回の急速成長により3上昇している。}}")]
    [TestCase(
        "{{G|+ This mutation's rank is increased by 2 due to being rapidly advanced 2 times.}}",
        "{{G|+ この変異のランクは2回の急速成長により2上昇している。}}")]
    [TestCase(
        "{{G|+ This mutation's rank is increased by 2 due to a metabolizing effect.}}",
        "{{G|+ この変異のランクは代謝効果により2上昇している。}}")]
    [TestCase(
        "{{G|+ This mutation's rank is increased by 2 due to a tonic effect.}}",
        "{{G|+ この変異のランクはトニック効果により2上昇している。}}")]
    [TestCase(
        "{{G|+ This mutation's rank is increased by 1 due to your equipped item, Stopsvalinn.}}",
        "{{G|+ この変異のランクは装備品 Stopsvalinn により1上昇している。}}")]
    [TestCase(
        "{{G|+ This mutation's rank is increased by 1 due to your equipped item, {{C|Stopsvalinn}}.}}",
        "{{G|+ この変異のランクは装備品 {{C|Stopsvalinn}} により1上昇している。}}")]
    [TestCase(
        "{{G|+ This mutation's rank is increased by 1 due to {{C|phase conjugate}}.}}",
        "{{G|+ この変異のランクは{{C|phase conjugate}}により1上昇している。}}")]
    [TestCase(
        "{{G|+ This mutation's rank is increased by 1 due to your {{C|neural lattice}}.}}",
        "{{G|+ この変異のランクはあなたの{{C|neural lattice}}により1上昇している。}}")]
    [TestCase(
        "{{G|+ This mutation's rank is increased by 1 due to phase conjugate.}}",
        "{{G|+ この変異のランクはphase conjugateにより1上昇している。}}")]
    [TestCase(
        "{{G|+ This mutation's rank is increased by 1 due to your neural lattice.}}",
        "{{G|+ この変異のランクはあなたのneural latticeにより1上昇している。}}")]
    [TestCase(
        "{{G|+ Mutation ranks cannot be reduced below 1.}}",
        "{{G|+ 変異ランクは1未満には下げられない。}}")]
    [TestCase(
        "{{R|- This mutation's rank is capped at 10 due to your level.}}",
        "{{R|- この変異のランクはあなたのレベルにより10に制限されている。}}")]
    public void ShowMutationPopup_TranslatesRankBoostReasonFamilies_PreservingColor(string sourceReason, string expectedReason)
    {
        AssertPopupMessage(
            ownerMethod: RequireMethod(
                typeof(DummyStatusScreenPopupTarget),
                nameof(DummyStatusScreenPopupTarget.ShowMutationPopup),
                typeof(DummyGameObject),
                typeof(DummyCharacterMutation)),
            "You have increased Force Wall's base rank to {{C|2}}!\n\n" + sourceReason,
            "力場壁の基本ランクを{{C|2}}に上げた！\n\n" + expectedReason);
    }

    [Test]
    public void ShowMutationPopup_RestoresOuterMutationScopeAfterNestedPopup()
    {
        object? outerState;
        StatusScreenMutationPopupTranslationPatch.Prefix(
            new DummyCharacterMutation { EntryName = "Force Wall", DisplayName = "Force Wall", Level = 1 },
            out outerState);
        try
        {
            object? innerState;
            StatusScreenMutationPopupTranslationPatch.Prefix(
                new DummyCharacterMutation { EntryName = "Light Manipulation", DisplayName = "Light Manipulation", Level = 1 },
                out innerState);
            try
            {
                var innerTranslated = StatusScreenMutationPopupTranslationPatch.TryTranslatePopupMessage(
                    "Light radius: 1",
                    nameof(PopupShowTranslationPatch),
                    "Popup.Show",
                    out var innerMessage);

                Assert.Multiple(() =>
                {
                    Assert.That(innerTranslated, Is.True);
                    Assert.That(innerMessage, Does.StartWith("光を操る。"));
                });
            }
            finally
            {
                _ = StatusScreenMutationPopupTranslationPatch.Finalizer(null, innerState);
            }

            var outerTranslated = StatusScreenMutationPopupTranslationPatch.TryTranslatePopupMessage(
                "You generate a wall of force.",
                nameof(PopupShowTranslationPatch),
                "Popup.Show",
                out var outerMessage);

            Assert.Multiple(() =>
            {
                Assert.That(outerTranslated, Is.True);
                Assert.That(outerMessage, Does.StartWith("力の壁を生み出し"));
            });
        }
        finally
        {
            _ = StatusScreenMutationPopupTranslationPatch.Finalizer(null, outerState);
        }
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

            DummyPopupShow.Show("{{C|You do not have enough mutation points to increase that mutation's rank.}}");

            Assert.That(
                DummyPopupShow.LastShowMessage,
                Is.EqualTo("{{C|You do not have enough mutation points to increase that mutation's rank.}}"));
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
        AssertPopupMessage(
            ownerMethod: RequireMethod(
                typeof(DummyStatusScreenPopupTarget),
                nameof(DummyStatusScreenPopupTarget.ShowMutationPopup),
                typeof(DummyGameObject),
                typeof(DummyCharacterMutation)),
            MessageFrameTranslator.MarkDirectTranslation(
                "{{C|You do not have enough mutation points to increase that mutation's rank.}}"),
            "{{C|You do not have enough mutation points to increase that mutation's rank.}}");
    }

    [Test]
    public void StatusScreenPopup_LeavesEmptyPopupUnchanged_WhenOwnerPatched()
    {
        AssertPopupMessage(
            ownerMethod: RequireMethod(typeof(DummyStatusScreenPopupTarget), nameof(DummyStatusScreenPopupTarget.BuyRandomMutation), typeof(DummyGameObject)),
            string.Empty,
            string.Empty);
        AssertPopupMessage(
            ownerMethod: RequireMethod(
                typeof(DummyStatusScreenPopupTarget),
                nameof(DummyStatusScreenPopupTarget.ShowMutationPopup),
                typeof(DummyGameObject),
                typeof(DummyCharacterMutation)),
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
            nameof(DummyStatusScreenPopupTarget.ShowMutationPopup) => new object[]
            {
                new DummyGameObject(),
                new DummyCharacterMutation { EntryName = "Force Wall", DisplayName = "Force Wall", Level = 1 },
            },
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
        if (original.Name != nameof(DummyStatusScreenPopupTarget.ShowMutationPopup))
        {
            return;
        }

        harmony.Patch(
            original: original,
            prefix: new HarmonyMethod(RequireMethod(typeof(StatusScreenMutationPopupTranslationPatch), nameof(StatusScreenMutationPopupTranslationPatch.Prefix), typeof(object), typeof(object).MakeByRefType())),
            finalizer: new HarmonyMethod(RequireMethod(typeof(StatusScreenMutationPopupTranslationPatch), nameof(StatusScreenMutationPopupTranslationPatch.Finalizer), typeof(Exception), typeof(object))));
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
