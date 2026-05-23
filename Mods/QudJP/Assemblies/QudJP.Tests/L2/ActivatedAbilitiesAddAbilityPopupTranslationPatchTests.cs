using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class ActivatedAbilitiesAddAbilityPopupTranslationPatchTests
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

    [TestCase(
        "You have gained the activated ability {{Y|Rifle through Trash}}.",
        "起動アビリティ {{Y|ゴミ漁り}} を得た。")]
    [TestCase(
        "You have gained the activated ability {{Y|Discharge [3 charge]}}.",
        "起動アビリティ {{Y|放電 [3チャージ]}} を得た。")]
    [TestCase(
        "You have gained the activated ability {{Y|Unknown Ability}}.",
        "起動アビリティ {{Y|Unknown Ability}} を得た。")]
    public void AddAbility_TranslatesGainedAbilityPopup_WhenOwnerPatched(string source, string expected)
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(ActivatedAbilitiesAddAbilityPopupTranslationPatch),
            RequireOwnerMethod(),
            () =>
            {
                new DummySkillsAndPowersSelectNodeTarget
                {
                    PopupMessageToShow = source,
                    PopupSurface = nameof(DummyPopupShow.Show),
                }.SelectNode();

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
                    Assert.That(RouteHitCount("GainedAbility"), Is.EqualTo(1));
                });
            });
    }

    [Test]
    public void AddAbility_TranslatesKeyboardHint_WhenOwnerPatched()
    {
        const string source = "You have gained the activated ability {{Y|Rifle through Trash}}.\n(press {{W|a}} to use activated abilities)";

        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(ActivatedAbilitiesAddAbilityPopupTranslationPatch),
            RequireOwnerMethod(),
            () =>
            {
                new DummySkillsAndPowersSelectNodeTarget
                {
                    PopupMessageToShow = source,
                    PopupSurface = nameof(DummyPopupShow.Show),
                }.SelectNode();

                Assert.That(
                    DummyPopupShow.LastShowMessage,
                    Is.EqualTo("起動アビリティ {{Y|ゴミ漁り}} を得た。\n（起動アビリティを使うには{{W|a}}を押す）"));
            });
    }

    [Test]
    public void AddAbility_DoesNotClaimPopupOnlyTraffic_WhenOwnerAbsent()
    {
        const string source = "You have gained the activated ability {{Y|Rifle through Trash}}.";

        OwnerPopupRouteTestHarness.WithPatchedPopupOnly(() => DummyPopupShow.Show(source));

        Assert.That(RouteHitCount("GainedAbility"), Is.Zero);
    }

    [Test]
    public void AddAbility_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        const string source = "You have gained the activated ability {{Y|Rifle through Trash}}.";

        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(ActivatedAbilitiesAddAbilityPopupTranslationPatch),
            RequireOwnerMethod(),
            () =>
            {
                new DummySkillsAndPowersSelectNodeTarget
                {
                    PopupMessageToShow = MessageFrameTranslator.MarkDirectTranslation(source),
                    PopupSurface = nameof(DummyPopupShow.Show),
                }.SelectNode();

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(source));
                    Assert.That(RouteHitCount("GainedAbility"), Is.Zero);
                });
            });
    }

    private static System.Reflection.MethodInfo RequireOwnerMethod()
    {
        return OwnerPopupRouteTestHarness.RequireMethod(
            typeof(DummySkillsAndPowersSelectNodeTarget),
            nameof(DummySkillsAndPowersSelectNodeTarget.SelectNode));
    }

    private static int RouteHitCount(string detail)
    {
        return OwnerPopupRouteTestHarness.RouteHitCount(
            typeof(ActivatedAbilitiesAddAbilityPopupTranslationPatch),
            detail);
    }
}
