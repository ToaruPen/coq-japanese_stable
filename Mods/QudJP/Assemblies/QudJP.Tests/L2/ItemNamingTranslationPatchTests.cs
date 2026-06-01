using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class ItemNamingTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();
        MessagePatternTranslator.ResetForTests();
        MessageFrameTranslator.ResetForTests();
        UseRepositoryVerbDictionary();
        DummyPopupShow.Reset();
        DummyPopupGenericTarget.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();
        MessagePatternTranslator.ResetForTests();
        MessageFrameTranslator.ResetForTests();
        Translator.ResetForTests();
    }

    [Test]
    public void Patch_TranslatesOpportunityPrompt_WhenOwnerPatched()
    {
        var target = new DummyItemNamingProducerTarget
        {
            PopupMessageToShow = "You swell with the inspiration to name your {{Y|銅の短剣}}. Do you wish to?",
        };

        WithPatchedOwner(
            () => InvokeOpportunity(target));

        Assert.Multiple(() =>
        {
            Assert.That(
                DummyPopupShow.LastShowYesNoMessage,
                Is.EqualTo("あなたは{{Y|銅の短剣}}に名付けたい衝動に駆られた。そうしますか？"));
            Assert.That(ItemNamingHitCount("Opportunity"), Is.EqualTo(1));
        });
    }

    [Test]
    public void TryTranslatePopupMessage_TranslatesMarkedCheckBestowals_WhenOwnerActive()
    {
        var doesFragment = DoesVerbRouteTranslator.MarkDoesFragment(
            "The 銅の短剣 seems",
            "seem",
            "The 銅の短剣".Length,
            null);
        var source = doesFragment + " to have taken on new qualities.";

        try
        {
            ItemNamingTranslationPatch.Prefix();

            var ok = ItemNamingTranslationPatch.TryTranslatePopupMessage(
                source,
                nameof(PopupShowTranslationPatch),
                "Popup.ProducerText",
                out var translated);

            Assert.Multiple(() =>
            {
                Assert.That(ok, Is.True);
                Assert.That(translated, Is.EqualTo("銅の短剣は新たな特質を帯びたようだ"));
                Assert.That(ItemNamingHitCount("CheckBestowals.DoesVerb"), Is.EqualTo(1));
            });
        }
        finally
        {
            _ = ItemNamingTranslationPatch.Finalizer(null);
        }
    }

    [Test]
    public void Patch_TranslatesMarkedCheckBestowalsPopup_WhenOwnerPatched()
    {
        var doesFragment = DoesVerbRouteTranslator.MarkDoesFragment(
            "The 銅の短剣 seems",
            "seem",
            "The 銅の短剣".Length,
            null);
        var target = new DummyItemNamingProducerTarget
        {
            PopupMessageToShow = doesFragment + " to have taken on new qualities.",
        };

        WithPatchedOwner(
            () =>
            {
                target.CheckBestowals(
                    new DummyGameObject(),
                    new DummyGameObject(),
                    null,
                    null,
                    null,
                    null,
                    "General",
                    out _,
                    out _,
                    out _);
            });

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("銅の短剣は新たな特質を帯びたようだ"));
            Assert.That(DummyPopupShow.LastShowMessage!.IndexOf('\u0002'), Is.EqualTo(-1));
            Assert.That(DummyPopupShow.LastShowMessage!.IndexOf('\u001f'), Is.EqualTo(-1));
            Assert.That(DummyPopupShow.LastShowMessage!.IndexOf('\u0003'), Is.EqualTo(-1));
            Assert.That(ItemNamingHitCount("CheckBestowals.DoesVerb"), Is.EqualTo(1));
        });
    }

    [Test]
    public void Patch_TranslatesNameItemPopup_WhenOwnerPatched()
    {
        var target = new DummyItemNamingProducerTarget
        {
            PopupMessageToShow = "You name {{Y|銅の短剣}} '{{C|暁}}'.",
        };

        WithPatchedOwner(
            () => InvokeNameItem(target));

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("あなたは{{Y|銅の短剣}}に「{{C|暁}}」と名付けた。"));
            Assert.That(ItemNamingHitCount("NameItem"), Is.EqualTo(1));
        });
    }

    [Test]
    public void Patch_ClaimsColorPickerPrompt_WhenOwnerPatched()
    {
        var target = new DummyItemNamingProducerTarget();

        WithPatchedOwner(
            () => InvokeInteractiveNameItem(target));

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupGenericTarget.LastShowColorPickerIntro, Is.EqualTo("{{Y|銅の短剣}}の名前として「{{C|暁}}」を選択した。色を選ぶ。"));
            Assert.That(ItemNamingHitCount(nameof(PopupShowColorPickerTranslationPatch), "Interactive.ColorPicker"), Is.EqualTo(1));
        });
    }

    [Test]
    public void Patch_TranslatesInteractiveNameItemPromptAndOptions_WhenOwnerPatched()
    {
        var target = new DummyItemNamingProducerTarget();

        WithPatchedOwner(
            () => InvokeInteractiveNameItem(target));

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupGenericTarget.LastPickOptionIntro, Is.EqualTo("{{Y|銅の短剣}}の名前を変更する。"));
            Assert.That(
                DummyPopupGenericTarget.LastPickOptionOptions,
                Is.EqualTo(new[]
                {
                    "名前を入力する。",
                    "特質に基づいて名前を付ける。",
                    "自分の文化からランダムな名前を選ぶ。",
                    "{{C|Barathrumites' culture}}からランダムな名前を選ぶ。",
                }));
            Assert.That(DummyPopupGenericTarget.LastAskStringMessage, Is.EqualTo("{{Y|銅の短剣}}の新しい名前を入力する。"));
            Assert.That(ItemNamingHitCount(nameof(PopupPickOptionTranslationPatch), "Interactive.Rename"), Is.EqualTo(1));
            Assert.That(ItemNamingMenuItemHitCount(nameof(PopupPickOptionTranslationPatch), "Interactive.EnterName"), Is.EqualTo(1));
            Assert.That(ItemNamingMenuItemHitCount(nameof(PopupPickOptionTranslationPatch), "Interactive.Qualities"), Is.EqualTo(1));
            Assert.That(ItemNamingMenuItemHitCount(nameof(PopupPickOptionTranslationPatch), "Interactive.OwnCulture"), Is.EqualTo(1));
            Assert.That(ItemNamingMenuItemHitCount(nameof(PopupPickOptionTranslationPatch), "Interactive.Culture"), Is.EqualTo(1));
            Assert.That(ItemNamingHitCount(nameof(PopupAskStringTranslationPatch), "Interactive.AskString"), Is.EqualTo(1));
        });
    }

    [Test]
    public void Patch_TranslatesItemNamingWishDebugPopup_WhenOwnerPatched()
    {
        var target = new DummyItemNamingProducerTarget
        {
            PopupMessageToShow =
                "[Debug: Created {{Y|snapjaw}} as kill.]\n" +
                "[Debug: Created {{C|mechanimist}} as InfluencedBy.]\n",
        };

        WithPatchedOwner(
            () => InvokeHandleItemNamingWish(target));

        Assert.Multiple(() =>
        {
            Assert.That(
                DummyPopupShow.LastShowMessage,
                Is.EqualTo(
                    "[Debug: {{Y|snapjaw}} を kill として作成した。]\n" +
                    "[Debug: {{C|mechanimist}} を InfluencedBy として作成した。]\n"));
            Assert.That(ItemNamingHitCount("WishDebugCreated"), Is.EqualTo(1));
        });
    }

    [Test]
    public void Patch_TranslatesItemNamingWishFailurePopup_WhenOwnerPatched()
    {
        var target = new DummyItemNamingProducerTarget
        {
            PopupMessageToShow = "[Debug: Naming failed.]",
        };

        WithPatchedOwner(
            () => InvokeHandleItemNamingWish(target));

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("[Debug: 命名に失敗した。]"));
            Assert.That(ItemNamingHitCount("WishDebugFailed"), Is.EqualTo(1));
        });
    }

    [Test]
    public void Patch_DoesNotClaimMarkedCheckBestowalsPopup_WhenOwnerAbsent()
    {
        var doesFragment = DoesVerbRouteTranslator.MarkDoesFragment(
            "The 銅の短剣 seems",
            "seem",
            "The 銅の短剣".Length,
            null);
        var message = doesFragment + " to have taken on new qualities.";

        PatchPopupShowOnly(() => DummyPopupShow.Show(message));

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("銅の短剣は新たな特質を帯びたようだ"));
            Assert.That(ItemNamingHitCount("CheckBestowals.DoesVerb"), Is.Zero);
        });
    }

    [Test]
    public void Patch_DoesNotClaimItemNamingPopup_WhenOwnerAbsent()
    {
        PatchPopupShowOnly(() =>
            DummyPopupShow.ShowYesNo("You swell with the inspiration to name your {{Y|銅の短剣}}. Do you wish to?"));

        Assert.That(ItemNamingHitCount("Opportunity"), Is.Zero);
    }

    [Test]
    public void Patch_DoesNotClaimNameItemPopup_WhenOwnerAbsent()
    {
        PatchPopupShowOnly(() => DummyPopupShow.Show("You name {{Y|銅の短剣}} '{{C|暁}}'."));

        Assert.That(ItemNamingHitCount("NameItem"), Is.Zero);
    }

    [Test]
    public void Patch_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        var source = MessageFrameTranslator.MarkDirectTranslation(
            "You swell with the inspiration to name your {{Y|銅の短剣}}. Do you wish to?");
        var target = new DummyItemNamingProducerTarget
        {
            PopupMessageToShow = source,
        };

        WithPatchedOwner(
            () => InvokeOpportunity(target));

        Assert.Multiple(() =>
        {
            Assert.That(
                DummyPopupShow.LastShowYesNoMessage,
                Is.EqualTo("You swell with the inspiration to name your {{Y|銅の短剣}}. Do you wish to?"));
            Assert.That(ItemNamingHitCount("Opportunity"), Is.Zero);
        });
    }

    [Test]
    public void Patch_DoesNotRetranslateDirectMarkedNameItemPopup_WhenOwnerPatched()
    {
        var source = MessageFrameTranslator.MarkDirectTranslation("You name {{Y|銅の短剣}} '{{C|暁}}'.");
        var target = new DummyItemNamingProducerTarget
        {
            PopupMessageToShow = source,
        };

        WithPatchedOwner(
            () => InvokeNameItem(target));

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("You name {{Y|銅の短剣}} '{{C|暁}}'."));
            Assert.That(ItemNamingHitCount("NameItem"), Is.Zero);
        });
    }

    [Test]
    public void Patch_LeavesEmptyPopupUnchanged_WhenOwnerPatched()
    {
        var target = new DummyItemNamingProducerTarget();

        WithPatchedOwner(
            () => InvokeOpportunity(target));

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupShow.LastShowYesNoMessage, Is.EqualTo(string.Empty));
            Assert.That(ItemNamingHitCount("Opportunity"), Is.Zero);
        });
    }

    private static void WithPatchedOwner(Action action)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchPopupShow(harmony);
            PatchPopupGenericRoutes(harmony);
            ItemNamingTranslationPatch.Prefix();
            action();
        }
        finally
        {
            _ = ItemNamingTranslationPatch.Finalizer(null);
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void PatchPopupShowOnly(Action action)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            PatchPopupShow(harmony);
            PatchPopupGenericRoutes(harmony);
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
            original: RequireMethod(typeof(DummyPopupShow), nameof(DummyPopupShow.Show)),
            prefix: new HarmonyMethod(RequireMethod(typeof(PopupShowTranslationPatch), nameof(PopupShowTranslationPatch.Prefix))));
        harmony.Patch(
            original: RequireMethod(typeof(DummyPopupShow), nameof(DummyPopupShow.ShowYesNo)),
            prefix: new HarmonyMethod(RequireMethod(typeof(PopupShowTranslationPatch), nameof(PopupShowTranslationPatch.Prefix))));
    }

    private static void PatchPopupGenericRoutes(Harmony harmony)
    {
        harmony.Patch(
            original: RequireMethod(typeof(DummyPopupGenericTarget), nameof(DummyPopupGenericTarget.PickOption)),
            prefix: new HarmonyMethod(RequireMethod(typeof(PopupPickOptionTranslationPatch), nameof(PopupPickOptionTranslationPatch.Prefix))),
            finalizer: new HarmonyMethod(RequireMethod(typeof(PopupPickOptionTranslationPatch), nameof(PopupPickOptionTranslationPatch.Finalizer))));
        harmony.Patch(
            original: RequireMethod(typeof(DummyPopupGenericTarget), nameof(DummyPopupGenericTarget.AskString)),
            prefix: new HarmonyMethod(RequireMethod(typeof(PopupAskStringTranslationPatch), nameof(PopupAskStringTranslationPatch.Prefix))));
        harmony.Patch(
            original: RequireMethod(typeof(DummyPopupGenericTarget), nameof(DummyPopupGenericTarget.ShowColorPicker)),
            prefix: new HarmonyMethod(RequireMethod(typeof(PopupShowColorPickerTranslationPatch), nameof(PopupShowColorPickerTranslationPatch.Prefix))));
    }

    private static MethodInfo RequireOwnerMethod(string methodName)
    {
        if (string.Equals(methodName, nameof(DummyItemNamingProducerTarget.Opportunity), StringComparison.Ordinal))
        {
            return RequireMethod(
                typeof(DummyItemNamingProducerTarget),
                methodName,
                typeof(DummyGameObject),
                typeof(DummyGameObject),
                typeof(DummyGameObject),
                typeof(string),
                typeof(string),
                typeof(int),
                typeof(int),
                typeof(int),
                typeof(int),
                typeof(bool));
        }

        if (string.Equals(methodName, nameof(DummyItemNamingProducerTarget.CheckBestowals), StringComparison.Ordinal))
        {
            return RequireMethod(
                typeof(DummyItemNamingProducerTarget),
                methodName,
                typeof(DummyGameObject),
                typeof(DummyGameObject),
                typeof(string),
                typeof(string),
                typeof(DummyGameObject),
                typeof(DummyGameObject),
                typeof(string),
                typeof(bool).MakeByRefType(),
                typeof(int).MakeByRefType(),
                typeof(bool).MakeByRefType());
        }

        if (string.Equals(methodName, nameof(DummyItemNamingProducerTarget.NameItem), StringComparison.Ordinal))
        {
            return RequireMethod(
                typeof(DummyItemNamingProducerTarget),
                methodName,
                typeof(DummyGameObject),
                typeof(DummyGameObject),
                typeof(string),
                typeof(string),
                typeof(string),
                typeof(string),
                typeof(bool),
                typeof(bool),
                typeof(DummyGameObject),
                typeof(DummyGameObject),
                typeof(string),
                typeof(bool),
                typeof(int),
                typeof(bool));
        }

        if (string.Equals(methodName, nameof(DummyItemNamingProducerTarget.HandleItemNamingWish), StringComparison.Ordinal))
        {
            return RequireMethod(
                typeof(DummyItemNamingProducerTarget),
                methodName,
                typeof(Match));
        }

        throw new InvalidOperationException($"Unhandled item naming owner method: {methodName}");
    }

    private static MethodInfo RequireInteractiveOwnerMethod()
    {
        return RequireMethod(
            typeof(DummyItemNamingProducerTarget),
            nameof(DummyItemNamingProducerTarget.NameItem),
            typeof(DummyGameObject),
            typeof(DummyGameObject),
            typeof(DummyGameObject),
            typeof(DummyGameObject),
            typeof(string),
            typeof(string),
            typeof(bool));
    }

    private static void InvokeOpportunity(DummyItemNamingProducerTarget target)
    {
        _ = RequireOwnerMethod(nameof(DummyItemNamingProducerTarget.Opportunity)).Invoke(
            target,
            new object?[]
            {
                new DummyGameObject(),
                null,
                null,
                null,
                "General",
                0,
                0,
                0,
                0,
                false,
            });
    }

    private static void InvokeNameItem(DummyItemNamingProducerTarget target)
    {
        _ = RequireOwnerMethod(nameof(DummyItemNamingProducerTarget.NameItem)).Invoke(
            target,
            new object?[]
            {
                new DummyGameObject(),
                new DummyGameObject(),
                "暁",
                "C",
                null,
                null,
                false,
                false,
                null,
                null,
                "General",
                false,
                0,
                false,
            });
    }

    private static void InvokeInteractiveNameItem(DummyItemNamingProducerTarget target)
    {
        _ = RequireInteractiveOwnerMethod().Invoke(
            target,
            new object?[]
            {
                new DummyGameObject(),
                new DummyGameObject(),
                null,
                null,
                null,
                "General",
                true,
            });
    }

    private static void InvokeHandleItemNamingWish(DummyItemNamingProducerTarget target)
    {
        _ = RequireOwnerMethod(nameof(DummyItemNamingProducerTarget.HandleItemNamingWish)).Invoke(
            target,
            new object?[]
            {
                Regex.Match("itemnaming", "^itemnaming$"),
            });
    }

    private static int ItemNamingHitCount(string detail)
    {
        return ItemNamingHitCount(nameof(PopupShowTranslationPatch), detail);
    }

    private static int ItemNamingHitCount(string route, string detail)
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            route,
            "Popup.ProducerText." + nameof(ItemNamingTranslationPatch) + "." + detail);
    }

    private static int ItemNamingMenuItemHitCount(string route, string detail)
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            route,
            "Popup.ProducerMenuItem." + nameof(ItemNamingTranslationPatch) + "." + detail);
    }

    private static void UseRepositoryVerbDictionary()
    {
        var repositoryRoot = QudJP.Tests.L1.TestProjectPaths.GetRepositoryRoot();
        Translator.SetDictionaryDirectoryForTests(Path.Combine(
            repositoryRoot,
            "Mods",
            "QudJP",
            "Localization",
            "Dictionaries"));
        var repositoryDictionaryPath = Path.Combine(
            repositoryRoot,
            "Mods",
            "QudJP",
            "Localization",
            "MessageFrames",
            "verbs.ja.json");

        MessageFrameTranslator.SetDictionaryPathForTests(repositoryDictionaryPath);
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

    private static string CreateHarmonyId()
    {
        return $"qudjp.tests.{Guid.NewGuid():N}";
    }
}
