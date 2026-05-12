using System.Reflection;
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
    }

    [TearDown]
    public void TearDown()
    {
        DynamicTextObservability.ResetForTests();
        SinkObservation.ResetForTests();
        MessagePatternTranslator.ResetForTests();
        MessageFrameTranslator.ResetForTests();
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

    private static int ItemNamingHitCount(string detail)
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            nameof(PopupShowTranslationPatch),
            "Popup.ProducerText." + nameof(ItemNamingTranslationPatch) + "." + detail);
    }

    private static void UseRepositoryVerbDictionary()
    {
        var repositoryDictionaryPath = Path.Combine(
            QudJP.Tests.L1.TestProjectPaths.GetRepositoryRoot(),
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
