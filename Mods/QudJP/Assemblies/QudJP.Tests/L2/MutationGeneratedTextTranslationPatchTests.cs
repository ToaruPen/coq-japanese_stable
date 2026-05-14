using System.Reflection;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class MutationGeneratedTextTranslationPatchTests
{
    private const string PhotosyntheticSkinOwner = "XRL.World.Parts.Mutation.PhotosyntheticSkin|HandleEvent";
    private const string LifeDrainOwner = "XRL.World.Parts.Mutation.LifeDrain|FireEvent";
    private const string PackRatOwner = "XRL.World.Parts.Mutation.PackRat|FireEvent";
    private const string BelcherOwner = "XRL.World.Parts.Mutation.Belcher|Cast";

    [SetUp]
    public void SetUp()
    {
        DummyPopupShow.Reset();
        DummyMessageQueue.Reset();
        DynamicTextObservability.ResetForTests();
        RuntimeDiagnostics.SetVerboseProbesEnabledForTests(true);
    }

    [TearDown]
    public void TearDown()
    {
        RuntimeDiagnostics.SetVerboseProbesEnabledForTests(null);
    }

    [TestCase(
        PhotosyntheticSkinOwner,
        "You start to metabolize the meal, gaining the following effect for the rest of the day:\n\n{{W|+5% to quickness}}",
        "食事を消化し始め、今日の残りの間、以下の効果を得る:\n\n{{W|+5% to quickness}}",
        "PhotosyntheticSkinMetabolize")]
    [TestCase(
        LifeDrainOwner,
        "You cannot syphon vim from yourself.",
        "自分自身からヴィムを吸い取れない。",
        "LifeDrainInvalidTarget")]
    [TestCase(
        LifeDrainOwner,
        "You cannot syphon vim from {{Y|snapjaw}}.",
        "{{Y|snapjaw}}からヴィムを吸い取れない。",
        "LifeDrainInvalidTarget")]
    [TestCase(
        PackRatOwner,
        "You must wait 3 more turns to work up the willpower to drop something!",
        "何かを落とす意志力を奮い立たせるにはあと3ターン待たなければならない！",
        "PackRatDropCooldown")]
    [TestCase(
        BelcherOwner,
        "That is out of range! (8 squares)",
        "射程外だ！(8マス)",
        "BelcherOutOfRange")]
    public void Patch_TranslatesMutationGeneratedOwnerPopups_WhenOwnerPatched(
        string ownerKey,
        string source,
        string expected,
        string detail)
    {
        AssertOwnerPopup(ownerKey, source, expected, detail, expectedHits: 1);
    }

    [Test]
    public void Patch_TranslatesPackRatGeneratedQueueMessage_WhenOwnerPatched()
    {
        AssertOwnerQueuedMessage(
            PackRatOwner,
            "&RYou must collect more junk! (minimum: 90 lbs.)",
            "&Rもっとガラクタを集めろ！（最低 90 ポンド）",
            "PackRatCollectMoreJunk",
            expectedHits: 1);
    }

    [Test]
    public void Patch_DoesNotClaimPopupOnlyGeneratedTraffic_WhenOwnerAbsent()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShow(harmony);
            PatchPopupShowFail(harmony);

            DummyPopupShow.ShowFail("You cannot syphon vim from {{Y|snapjaw}}.");

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo("You cannot syphon vim from {{Y|snapjaw}}."));
                Assert.That(PopupHitCount("LifeDrainInvalidTarget"), Is.EqualTo(0));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Patch_DoesNotClaimQueueOnlyGeneratedTraffic_WhenOwnerAbsent()
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);

            DummyMessageQueue.AddPlayerMessage("&RYou must collect more junk! (minimum: 90 lbs.)", Capitalize: false);

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("&RYou must collect more junk! (minimum: 90 lbs.)"));
                Assert.That(QueueHitCount("PackRatCollectMoreJunk"), Is.EqualTo(0));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void Patch_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        AssertOwnerPopup(
            LifeDrainOwner,
            MessageFrameTranslator.MarkDirectTranslation("You cannot syphon vim from yourself."),
            "You cannot syphon vim from yourself.",
            "LifeDrainInvalidTarget",
            expectedHits: 0);
    }

    [Test]
    public void Patch_DoesNotRetranslateDirectMarkedQueueMessage_WhenOwnerPatched()
    {
        AssertOwnerQueuedMessage(
            PackRatOwner,
            MessageFrameTranslator.MarkDirectTranslation("&RYou must collect more junk! (minimum: 90 lbs.)"),
            "&RYou must collect more junk! (minimum: 90 lbs.)",
            "PackRatCollectMoreJunk",
            expectedHits: 0);
    }

    [TestCase(LifeDrainOwner, "You can syphon vim from {{Y|snapjaw}}.")]
    [TestCase(PackRatOwner, "You must wait three more turns to work up the willpower to drop something!")]
    [TestCase(BelcherOwner, "That is out of range! (nearby)")]
    [TestCase(PhotosyntheticSkinOwner, "You start to metabolize the meal.")]
    [TestCase(LifeDrainOwner, "")]
    public void Patch_LeavesUnsupportedPopupsUnchanged_WhenOwnerPatched(string ownerKey, string source)
    {
        AssertOwnerPopup(ownerKey, source, source, "Unsupported", expectedHits: 0);
    }

    [TestCase("")]
    [TestCase("You must collect more junk soon.")]
    public void Patch_LeavesUnsupportedQueuedMessagesUnchanged_WhenOwnerPatched(string source)
    {
        AssertOwnerQueuedMessage(PackRatOwner, source, source, "PackRatCollectMoreJunk", expectedHits: 0);
    }

    private static void AssertOwnerPopup(
        string ownerKey,
        string source,
        string expected,
        string detail,
        int expectedHits)
    {
        var ownerRoute = CreateOwnerRouteFromKey(ownerKey);
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchPopupShow(harmony);
            PatchPopupShowFail(harmony);
            PatchOwner(harmony, ownerRoute.Method);

            ownerRoute.Invoke(() =>
            {
                if (string.Equals(ownerKey, LifeDrainOwner, StringComparison.Ordinal))
                {
                    DummyPopupShow.ShowFail(source);
                }
                else
                {
                    DummyPopupShow.Show(source);
                }
            });

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
                if (!string.Equals(detail, "Unsupported", StringComparison.Ordinal))
                {
                    Assert.That(PopupHitCount(detail), Is.EqualTo(expectedHits));
                }
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void AssertOwnerQueuedMessage(
        string ownerKey,
        string source,
        string expected,
        string detail,
        int expectedHits)
    {
        var ownerRoute = CreateOwnerRouteFromKey(ownerKey);
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);
        try
        {
            PatchQueue(harmony);
            PatchOwner(harmony, ownerRoute.Method);

            ownerRoute.Invoke(() => DummyMessageQueue.AddPlayerMessage(source, Capitalize: false));

            Assert.Multiple(() =>
            {
                Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo(expected));
                Assert.That(QueueHitCount(detail), Is.EqualTo(expectedHits));
            });
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
    }

    private static void PatchPopupShowFail(Harmony harmony)
    {
        harmony.Patch(
            original: RequireMethod(typeof(DummyPopupShow), nameof(DummyPopupShow.ShowFail)),
            prefix: new HarmonyMethod(RequireMethod(typeof(PopupShowTranslationPatch), nameof(PopupShowTranslationPatch.Prefix))));
    }

    private static void PatchQueue(Harmony harmony)
    {
        var original = RequireMethod(
            typeof(DummyMessageQueue),
            nameof(DummyMessageQueue.AddPlayerMessage),
            typeof(string),
            typeof(string),
            typeof(bool));
        harmony.Patch(
            original: original,
            prefix: new HarmonyMethod(RequireMethod(
                typeof(CombatAndLogMessageQueuePatch),
                nameof(CombatAndLogMessageQueuePatch.Prefix),
                typeof(string).MakeByRefType(),
                typeof(string),
                typeof(bool))));
        harmony.Patch(
            original: original,
            prefix: new HarmonyMethod(RequireMethod(
                typeof(MessageLogPatch),
                nameof(MessageLogPatch.Prefix),
                typeof(string).MakeByRefType(),
                typeof(string),
                typeof(bool))));
    }

    private static void PatchOwner(Harmony harmony, MethodInfo ownerMethod)
    {
        harmony.Patch(
            original: ownerMethod,
            prefix: new HarmonyMethod(RequireMethod(
                typeof(MutationGeneratedTextTranslationPatch),
                nameof(MutationGeneratedTextTranslationPatch.Prefix),
                typeof(MethodBase))),
            finalizer: new HarmonyMethod(RequireMethod(
                typeof(MutationGeneratedTextTranslationPatch),
                nameof(MutationGeneratedTextTranslationPatch.Finalizer),
                typeof(Exception))));
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

    private static DynamicOwnerRouteMethod CreateOwnerRouteFromKey(string ownerKey)
    {
        var separator = ownerKey.LastIndexOf('|');
        return DynamicOwnerRouteMethod.Create(ownerKey[..separator], ownerKey[(separator + 1)..]);
    }

    private static int PopupHitCount(string detail)
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            nameof(PopupShowTranslationPatch),
            "Popup.Show.MutationGeneratedTextTranslationPatch." + detail);
    }

    private static int QueueHitCount(string detail)
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            "MessageQueue.AddPlayerMessage",
            "MutationGeneratedTextTranslationPatch." + detail);
    }

    private static string CreateHarmonyId()
    {
        return $"qudjp.tests.{Guid.NewGuid():N}";
    }
}
