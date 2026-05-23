using QudJP.Patches;
using QudJP.Tests.DummyTargets;
using HarmonyLib;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class SkillsAndPowersSelectNodePopupTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        DynamicTextObservability.ResetForTests();
        DummyPopupShow.Reset();
        LocalizationAssetResolver.SetLocalizationRootForTests(
            Path.Combine(L1.TestProjectPaths.GetRepositoryRoot(), "Mods", "QudJP", "Localization"));
    }

    [TearDown]
    public void TearDown()
    {
        DummyPopupShow.Reset();
        DynamicTextObservability.ResetForTests();
        LocalizationAssetResolver.SetLocalizationRootForTests(null);
    }

    [TestCase("You already have that skill.", "そのスキルはすでに習得している。", "AlreadyHave", nameof(DummyPopupShow.Show))]
    [TestCase("You already have that power.", "そのパワーはすでに習得している。", "AlreadyHave", nameof(DummyPopupShow.Show))]
    [TestCase("You must be initiated into this skill in order to learn it.", "このスキルを習得するには入門している必要がある。", "InitiationRequired", nameof(DummyPopupShow.Show))]
    [TestCase("You must be initiated into this power in order to learn it.", "このパワーを習得するには入門している必要がある。", "InitiationRequired", nameof(DummyPopupShow.Show))]
    [TestCase("You don't have enough skill points to buy that skill!", "そのスキルを購入するにはスキルポイントが足りない！", "NotEnoughSkillPoints", nameof(DummyPopupShow.Show))]
    [TestCase("You don't have enough skill points to buy that power!", "そのパワーを購入するにはスキルポイントが足りない！", "NotEnoughSkillPoints", nameof(DummyPopupShow.Show))]
    [TestCase("You do not have the skill associated with that power. Would you like to purchase the required skill?", "そのパワーに関連するスキルを持っていない。前提スキルを購入しますか？", "RequiredSkillPrompt", nameof(DummyPopupShow.ShowYesNoCancel))]
    [TestCase("No implementation for XRL.World.Parts.Skill.LongBlades", "XRL.World.Parts.Skill.LongBladesの実装がない。", "NoImplementation", nameof(DummyPopupShow.Show))]
    [TestCase("Are you sure you want to buy Long Blade for {{C|150}} sp?", "長剣を{{C|150}}SPで購入しますか？", "BuyConfirmation", nameof(DummyPopupShow.ShowYesNo))]
    [TestCase("Are you sure you want to buy Tinker II for {{C|200}} sp?", "工匠 IIを{{C|200}}SPで購入しますか？", "BuyConfirmation", nameof(DummyPopupShow.ShowYesNo))]
    public void Patch_TranslatesSelectNodePopups_WhenOwnerPatched(
        string source,
        string expected,
        string detail,
        string popupSurface)
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(SkillsAndPowersSelectNodePopupTranslationPatch),
            RequireOwnerMethod(),
            () =>
            {
                new DummySkillsAndPowersSelectNodeTarget
                {
                    PopupMessageToShow = source,
                    PopupSurface = popupSurface,
                }.SelectNode();

                Assert.Multiple(() =>
                {
                    Assert.That(LastPopupMessage(popupSurface), Is.EqualTo(expected));
                    Assert.That(RouteHitCount(detail), Is.EqualTo(1));
                });
            });
    }

    [Test]
    public void Patch_PreservesColorMarkupInBuyConfirmation_WhenOwnerPatched()
    {
        const string source = "Are you sure you want to buy {{G|Long Blade}} for {{C|150}} sp?";

        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(SkillsAndPowersSelectNodePopupTranslationPatch),
            RequireOwnerMethod(),
            () =>
            {
                new DummySkillsAndPowersSelectNodeTarget
                {
                    PopupMessageToShow = source,
                    PopupSurface = nameof(DummyPopupShow.ShowYesNo),
                }.SelectNode();

                Assert.That(DummyPopupShow.LastShowYesNoMessage, Is.EqualTo("{{G|長剣}}を{{C|150}}SPで購入しますか？"));
            });
    }

    [Test]
    public void TranslateProducedMessage_MarksObservedNotEnoughSkillPointsForOwnerSinks()
    {
        var result = SkillsAndPowersSelectNodePopupTranslationPatch.TranslateProducedMessage(
            "You don't have enough skill points to buy that power!");

        Assert.Multiple(() =>
        {
            Assert.That(MessageFrameTranslator.TryStripDirectTranslationMarker(result, out var stripped), Is.True);
            Assert.That(stripped, Is.EqualTo("そのパワーを購入するにはスキルポイントが足りない！"));
            Assert.That(OwnerRouteHitCount("NotEnoughSkillPoints"), Is.EqualTo(1));
        });
    }

    [Test]
    public void TranslateProducedMessage_PreservesColorTagsAndMarksTranslatedText()
    {
        var result = SkillsAndPowersSelectNodePopupTranslationPatch.TranslateProducedMessage(
            "{{y|You don't have enough skill points to buy that skill!}}");

        Assert.Multiple(() =>
        {
            Assert.That(MessageFrameTranslator.TryStripDirectTranslationMarker(result, out var stripped), Is.True);
            Assert.That(stripped, Is.EqualTo("{{y|そのスキルを購入するにはスキルポイントが足りない！}}"));
        });
    }

    [Test]
    public void TranslateProducedMessage_LeavesUnknownYouDontHaveMessageUnclaimed()
    {
        const string source = "You don't have any schematics.";

        var result = SkillsAndPowersSelectNodePopupTranslationPatch.TranslateProducedMessage(source);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(source));
            Assert.That(MessageFrameTranslator.TryStripDirectTranslationMarker(result, out _), Is.False);
            Assert.That(OwnerRouteHitCount("NotEnoughSkillPoints"), Is.Zero);
        });
    }

    [Test]
    public void Transpiler_MarksGeneratedNotEnoughSkillPointsBeforeMessageLogSink()
    {
        RunWithProducerTranspilerAndMessageLogSink(
            nameof(DummySkillsAndPowersSelectNodeTarget.SelectNodeNotEnoughSkillPointsMessageLog),
            target => target.SelectNodeNotEnoughSkillPointsMessageLog());

        Assert.Multiple(() =>
        {
            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("そのパワーを購入するにはスキルポイントが足りない！"));
            Assert.That(OwnerRouteHitCount("NotEnoughSkillPoints"), Is.EqualTo(1));
        });
    }

    [Test]
    public void Transpiler_MarksFixedRequiredSkillPromptBeforeMessageLogSink()
    {
        RunWithProducerTranspilerAndMessageLogSink(
            nameof(DummySkillsAndPowersSelectNodeTarget.SelectNodeRequiredSkillPromptMessageLog),
            _ => DummySkillsAndPowersSelectNodeTarget.SelectNodeRequiredSkillPromptMessageLog());

        Assert.Multiple(() =>
        {
            Assert.That(DummyMessageQueue.LastMessage, Is.EqualTo("そのパワーに関連するスキルを持っていない。前提スキルを購入しますか？"));
            Assert.That(OwnerRouteHitCount("RequiredSkillPrompt"), Is.EqualTo(1));
        });
    }

    [Test]
    public void Patch_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent()
    {
        const string source = "You already have that skill.";

        OwnerPopupRouteTestHarness.WithPatchedPopupOnly(() => DummyPopupShow.Show(source));

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(source));
            Assert.That(RouteHitCount("AlreadyHave"), Is.Zero);
        });
    }

    [Test]
    public void Patch_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        const string source = "You already have that skill.";

        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(SkillsAndPowersSelectNodePopupTranslationPatch),
            RequireOwnerMethod(),
            () =>
            {
                new DummySkillsAndPowersSelectNodeTarget
                {
                    PopupMessageToShow = MessageFrameTranslator.MarkDirectTranslation(source),
                }.SelectNode();

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(source));
                    Assert.That(RouteHitCount("AlreadyHave"), Is.Zero);
                });
            });
    }

    [Test]
    public void Patch_LeavesEmptyPopupUnchanged_WhenOwnerPatched()
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(SkillsAndPowersSelectNodePopupTranslationPatch),
            RequireOwnerMethod(),
            () =>
            {
                new DummySkillsAndPowersSelectNodeTarget
                {
                    PopupMessageToShow = string.Empty,
                }.SelectNode();

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.Empty);
                    Assert.That(RouteHitCount("AlreadyHave"), Is.Zero);
                });
            });
    }

    private static string? LastPopupMessage(string popupSurface)
    {
        return popupSurface switch
        {
            nameof(DummyPopupShow.ShowYesNo) => DummyPopupShow.LastShowYesNoMessage,
            nameof(DummyPopupShow.ShowYesNoCancel) => DummyPopupShow.LastShowYesNoCancelMessage,
            _ => DummyPopupShow.LastShowMessage,
        };
    }

    private static System.Reflection.MethodInfo RequireOwnerMethod()
    {
        return OwnerPopupRouteTestHarness.RequireMethod(typeof(DummySkillsAndPowersSelectNodeTarget), nameof(DummySkillsAndPowersSelectNodeTarget.SelectNode));
    }

    private static int RouteHitCount(string detail)
    {
        return OwnerPopupRouteTestHarness.RouteHitCount(
            typeof(SkillsAndPowersSelectNodePopupTranslationPatch),
            detail);
    }

    private static int OwnerRouteHitCount(string detail)
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            nameof(SkillsAndPowersSelectNodePopupTranslationPatch),
            "Owner.ProducerText." + nameof(SkillsAndPowersSelectNodePopupTranslationPatch) + "." + detail);
    }

    private static void RunWithProducerTranspilerAndMessageLogSink(
        string ownerMethodName,
        Action<DummySkillsAndPowersSelectNodeTarget> action)
    {
        var harmonyId = $"qudjp.tests.{Guid.NewGuid():N}";
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: OwnerPopupRouteTestHarness.RequireMethod(
                    typeof(DummySkillsAndPowersSelectNodeTarget),
                    ownerMethodName),
                transpiler: new HarmonyMethod(OwnerPopupRouteTestHarness.RequireMethod(
                    typeof(SkillsAndPowersSelectNodePopupTranslationPatch),
                    nameof(SkillsAndPowersSelectNodePopupTranslationPatch.Transpiler))));
            harmony.Patch(
                original: OwnerPopupRouteTestHarness.RequireMethod(typeof(DummyMessageQueue), nameof(DummyMessageQueue.AddPlayerMessage)),
                prefix: new HarmonyMethod(OwnerPopupRouteTestHarness.RequireMethod(
                    typeof(MessageLogPatch),
                    nameof(MessageLogPatch.Prefix))));

            action(new DummySkillsAndPowersSelectNodeTarget());
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }
}
