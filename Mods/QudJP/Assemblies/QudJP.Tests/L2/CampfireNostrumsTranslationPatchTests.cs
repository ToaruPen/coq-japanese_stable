using System.Reflection;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class CampfireNostrumsTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        DummyPopupShow.Reset();
        DynamicTextObservability.ResetForTests();
        RuntimeDiagnostics.SetVerboseProbesEnabledForTests(true);
    }

    [TearDown]
    public void TearDown()
    {
        RuntimeDiagnostics.SetVerboseProbesEnabledForTests(null);
    }

    [TestCase(
        nameof(DummyCampfireNostrumsTarget.NostrumsStopBleeding),
        "You try to staunch the wounds of {{C|salt kraken}}, but your limbs pass through them.",
        "{{C|salt kraken}}の傷を止血しようとするが、手が体をすり抜ける。",
        "StaunchPassThrough")]
    [TestCase(
        nameof(DummyCampfireNostrumsTarget.NostrumsStopBleeding),
        "You try to staunch the wounds of frozen cherub, but cannot affect them.",
        "frozen cherubの傷を止血しようとするが、影響を与えられない。",
        "StaunchCannotAffect")]
    [TestCase(
        nameof(DummyCampfireNostrumsTarget.NostrumsStopBleeding),
        "You staunch the wounds of {{Y|warden}}, though some are too deep to treat.",
        "{{Y|warden}}の傷を止血したが、深すぎて処置できないものもある。",
        "StaunchPartial")]
    [TestCase(
        nameof(DummyCampfireNostrumsTarget.NostrumsStopBleeding),
        "You staunch the wounds of goatfolk pariah.",
        "goatfolk pariahの傷を止血した。",
        "StaunchFull")]
    [TestCase(
        nameof(DummyCampfireNostrumsTarget.NostrumsStopBleeding),
        "{{Y|warden}}'s wounds are too deep to treat.",
        "{{Y|warden}}'s woundsは深すぎて処置できない。",
        "WoundsTooDeep")]
    [TestCase(
        nameof(DummyCampfireNostrumsTarget.NostrumsStopBleeding),
        "Neither you nor {{M|Eskhind}} are bleeding.",
        "あなたも{{M|Eskhind}}も出血していない。",
        "NeitherBleeding")]
    [TestCase(
        nameof(DummyCampfireNostrumsTarget.NostrumsStopBleeding),
        "You are not bleeding.",
        "あなたは出血していない。",
        "NotBleeding")]
    [TestCase(
        nameof(DummyCampfireNostrumsTarget.NostrumsTreatPoison),
        "You have no medicinal ingredients with which to treat the poison coursing through snapjaw scavenger.",
        "snapjaw scavengerを蝕む毒を治療する薬用素材がない。",
        "NoMedicinalIngredients")]
    [TestCase(
        nameof(DummyCampfireNostrumsTarget.NostrumsTreatPoison),
        "You try to cure the poison coursing through {{G|salt weep}}, but your limbs pass through them.",
        "{{G|salt weep}}を蝕む毒を治そうとするが、手が体をすり抜ける。",
        "PoisonPassThrough")]
    [TestCase(
        nameof(DummyCampfireNostrumsTarget.NostrumsTreatPoison),
        "You try to cure the poison coursing through frozen cherub, but cannot affect them.",
        "frozen cherubを蝕む毒を治そうとするが、影響を与えられない。",
        "PoisonCannotAffect")]
    [TestCase(
        nameof(DummyCampfireNostrumsTarget.NostrumsTreatPoison),
        "You cure the poisons coursing through {{G|snapjaw scavenger}} with a balm made from {{Y|witchwood bark}}.",
        "{{Y|witchwood bark}}で作った塗り薬で{{G|snapjaw scavenger}}を蝕む毒を治した。",
        "CurePoison")]
    [TestCase(
        nameof(DummyCampfireNostrumsTarget.NostrumsTreatPoison),
        "You try to cure the poison coursing through goatfolk hero, but your cures are ineffective.",
        "goatfolk heroを蝕む毒を治そうとするが、治療が効かない。",
        "PoisonIneffective")]
    [TestCase(
        nameof(DummyCampfireNostrumsTarget.NostrumsTreatPoison),
        "The poison affecting you and {{G|snapjaw}} is too strong to be cured by your nostrums.",
        "あなたと{{G|snapjaw}}にかかった毒は、薬では治せないほど強い。",
        "PoisonTooStrongYouAndTarget")]
    [TestCase(
        nameof(DummyCampfireNostrumsTarget.NostrumsTreatPoison),
        "The poison affecting {{G|snapjaw}} and {{Y|glowfish}} is too strong to be cured by your nostrums.",
        "{{G|snapjaw}} and {{Y|glowfish}}にかかった毒は、薬では治せないほど強い。",
        "PoisonTooStrongTargets")]
    [TestCase(
        nameof(DummyCampfireNostrumsTarget.NostrumsTreatPoison),
        "Neither you nor {{M|Eskhind}} are poisoned.",
        "あなたも{{M|Eskhind}}も毒状態ではない。",
        "NeitherPoisoned")]
    [TestCase(
        nameof(DummyCampfireNostrumsTarget.NostrumsTreatIllness),
        "You have no medicinal ingredients with which to treat {{C|salt kraken}}'s illness.",
        "{{C|salt kraken}}'s illnessを治療する薬用素材がない。",
        "IllnessNoMedicinalIngredients")]
    [TestCase(
        nameof(DummyCampfireNostrumsTarget.NostrumsTreatIllness),
        "You try to cure {{C|salt kraken}}'s illness, but your limbs pass through them.",
        "{{C|salt kraken}}'s illnessを治そうとするが、手が体をすり抜ける。",
        "IllnessPassThrough")]
    [TestCase(
        nameof(DummyCampfireNostrumsTarget.NostrumsTreatIllness),
        "You try to {{Y|glowfish}}'s illness, but cannot affect them.",
        "{{Y|glowfish}}'s illnessを治そうとするが、影響を与えられない。",
        "IllnessCannotAffect")]
    [TestCase(
        nameof(DummyCampfireNostrumsTarget.NostrumsTreatIllness),
        "You cure {{G|snapjaw scavenger}}'s illness with a balm made from {{Y|witchwood bark}}.",
        "{{Y|witchwood bark}}で作った塗り薬で{{G|snapjaw scavenger}}'s illnessを治した。",
        "CureIllness")]
    [TestCase(
        nameof(DummyCampfireNostrumsTarget.NostrumsTreatIllness),
        "Neither you nor {{M|Eskhind}} are ill.",
        "あなたも{{M|Eskhind}}も病気ではない。",
        "NeitherIll")]
    [TestCase(
        nameof(DummyCampfireNostrumsTarget.NostrumsTreatDiseaseOnset),
        "{{C|salt kraken}} already has boosted immunity from a nostrum.",
        "{{C|salt kraken}}はすでに薬で免疫を高めている。",
        "DiseaseAlreadyBoosted")]
    [TestCase(
        nameof(DummyCampfireNostrumsTarget.NostrumsTreatDiseaseOnset),
        "You have no medicinal ingredients with which to treat {{C|salt kraken}}'s sore throat.",
        "{{C|salt kraken}}'s sore throatを治療する薬用素材がない。",
        "DiseaseNoMedicinalIngredients")]
    [TestCase(
        nameof(DummyCampfireNostrumsTarget.NostrumsTreatDiseaseOnset),
        "You try to cure {{C|salt kraken}}'s diease onset, but your limbs pass through them.",
        "{{C|salt kraken}}'s diease onsetを治そうとするが、手が体をすり抜ける。",
        "DiseasePassThrough")]
    [TestCase(
        nameof(DummyCampfireNostrumsTarget.NostrumsTreatDiseaseOnset),
        "You try to {{Y|glowfish}}'s disease onset, but cannot affect them.",
        "{{Y|glowfish}}'s disease onsetを治そうとするが、影響を与えられない。",
        "DiseaseCannotAffect")]
    [TestCase(
        nameof(DummyCampfireNostrumsTarget.NostrumsTreatDiseaseOnset),
        "You cure {{G|snapjaw scavenger}}'s sore throat with a balm made from {{Y|witchwood bark}}.",
        "{{Y|witchwood bark}}で作った塗り薬で{{G|snapjaw scavenger}}'s sore throatを治した。",
        "CureDiseaseOnset")]
    [TestCase(
        nameof(DummyCampfireNostrumsTarget.NostrumsTreatDiseaseOnset),
        "You boost {{G|snapjaw scavenger}}'s immunity with a balm made from {{Y|witchwood bark}}.",
        "{{Y|witchwood bark}}で作った塗り薬で{{G|snapjaw scavenger}}'s immunityを高めた。",
        "BoostImmunity")]
    [TestCase(
        nameof(DummyCampfireNostrumsTarget.NostrumsTreatDiseaseOnset),
        "You try to boost {{G|snapjaw scavenger}}'s immunity with a balm made from {{Y|witchwood bark}}, but it is ineffective.",
        "{{Y|witchwood bark}}で作った塗り薬で{{G|snapjaw scavenger}}'s immunityを高めようとするが、効果がない。",
        "BoostImmunityIneffective")]
    [TestCase(
        nameof(DummyCampfireNostrumsTarget.NostrumsTreatDiseaseOnset),
        "Neither you nor {{M|Eskhind}} are suffering from the onset of a disease.",
        "あなたも{{M|Eskhind}}も病気の発症に苦しんでいない。",
        "NeitherDiseaseOnset")]
    public void Patch_TranslatesCampfireNostrumsPopups_WhenOwnerPatched(
        string ownerMethodName,
        string source,
        string expected,
        string detail)
    {
        AssertOwnerPopup(ownerMethodName, source, expected, detail, expectedHits: 1);
    }

    [Test]
    public void Patch_DoesNotRecordCampfireNostrumsRoute_WhenOwnerAbsent()
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOnly(() =>
        {
            DummyPopupShow.ShowFail(
                "The poison affecting you and {{G|snapjaw}} is too strong to be cured by your nostrums.");

            Assert.Multiple(() =>
            {
                Assert.That(
                    DummyPopupShow.LastShowMessage,
                    Is.EqualTo("The poison affecting you and {{G|snapjaw}} is too strong to be cured by your nostrums."));
                Assert.That(HitCount("PoisonTooStrongYouAndTarget"), Is.EqualTo(0));
            });
        });
    }

    [Test]
    public void Patch_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        AssertOwnerPopup(
            nameof(DummyCampfireNostrumsTarget.NostrumsStopBleeding),
            MessageFrameTranslator.MarkDirectTranslation("Neither you nor {{M|Eskhind}} are bleeding."),
            "Neither you nor {{M|Eskhind}} are bleeding.",
            "NeitherBleeding",
            expectedHits: 0);
    }

    [TestCase("Treat whom first?", "最初に誰を治療する？")]
    [TestCase("Treat whom next?", "次に誰を治療する？")]
    [TestCase("Select an ingredient to use.", "使う材料を選ぶ。")]
    [TestCase("{{W|Treat whom first?}}", "{{W|最初に誰を治療する？}}")]
    public void Patch_TranslatesPickGameObjectTitles_WhenOwnerActive(string source, string expected)
    {
        CampfireNostrumsTranslationPatch.Prefix();
        try
        {
            var translated = PopupTranslationPatch.TranslatePopupTextForProducerRoute(
                source,
                nameof(PopupPickOptionTranslationPatch));

            Assert.Multiple(() =>
            {
                Assert.That(translated, Is.EqualTo(expected));
                Assert.That(PickGameObjectTitleHitCount(), Is.EqualTo(1));
            });
        }
        finally
        {
            CampfireNostrumsTranslationPatch.Finalizer(null);
        }
    }

    [Test]
    public void Patch_TranslatesDirectMarkedPickGameObjectTitle_WhenOwnerActive()
    {
        CampfireNostrumsTranslationPatch.Prefix();
        try
        {
            var handled = CampfireNostrumsTranslationPatch.TryTranslatePopupProducerText(
                MessageFrameTranslator.MarkDirectTranslation("Treat whom first?"),
                nameof(PopupPickOptionTranslationPatch),
                "Popup.ProducerText",
                out var translated);

            Assert.Multiple(() =>
            {
                Assert.That(handled, Is.True);
                Assert.That(translated, Is.EqualTo("最初に誰を治療する？"));
                Assert.That(PickGameObjectTitleHitCount(), Is.EqualTo(1));
            });
        }
        finally
        {
            CampfireNostrumsTranslationPatch.Finalizer(null);
        }
    }

    [Test]
    public void Patch_DoesNotTranslatePickGameObjectTitle_WhenOwnerAbsent()
    {
        const string source = "Treat whom first?";

        var handled = CampfireNostrumsTranslationPatch.TryTranslatePopupProducerText(
            source,
            nameof(PopupPickOptionTranslationPatch),
            "Popup.ProducerText",
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(handled, Is.False);
            Assert.That(translated, Is.EqualTo(source));
            Assert.That(PickGameObjectTitleHitCount(), Is.Zero);
        });
    }

    [Test]
    public void Patch_LeavesEmptyPopupUnchanged_WhenOwnerPatched()
    {
        AssertOwnerPopup(
            nameof(DummyCampfireNostrumsTarget.NostrumsStopBleeding),
            string.Empty,
            string.Empty,
            "StaunchPassThrough",
            expectedHits: 0);
    }

    private static void AssertOwnerPopup(
        string ownerMethodName,
        string source,
        string expected,
        string detail,
        int expectedHits)
    {
        var ownerMethod = RequireOwnerMethod(ownerMethodName);
        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(CampfireNostrumsTranslationPatch),
            ownerMethod,
            () =>
            {
                var target = new DummyCampfireNostrumsTarget
                {
                    PopupMessageToSend = source,
                    UseFailurePopup = ShouldUseFailurePopup(detail),
                };

                _ = ownerMethod.Invoke(target, Array.Empty<object>());

                Assert.Multiple(() =>
                {
                    Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
                    Assert.That(HitCount(detail), Is.EqualTo(expectedHits));
                });
            });
    }

    private static int HitCount(string detail)
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            nameof(PopupShowTranslationPatch),
            "Popup.ProducerText." + nameof(CampfireNostrumsTranslationPatch) + "." + detail);
    }

    private static int PickGameObjectTitleHitCount()
    {
        return DynamicTextObservability.GetRouteFamilyHitCountForTests(
            nameof(PopupPickOptionTranslationPatch),
            "Popup.ProducerText." + nameof(CampfireNostrumsTranslationPatch) + ".PickGameObjectTitle");
    }

    private static bool ShouldUseFailurePopup(string detail)
    {
        return detail is not ("CurePoison" or "CureIllness" or "CureDiseaseOnset" or "BoostImmunity"
            or "BoostImmunityIneffective");
    }

    private static MethodInfo RequireOwnerMethod(string methodName)
    {
        return OwnerPopupRouteTestHarness.RequireMethod(typeof(DummyCampfireNostrumsTarget), methodName);
    }
}
