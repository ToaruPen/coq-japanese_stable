using System.Runtime.CompilerServices;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;
using QudJP.Tests.L1;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class ConversationRewardPopupTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        DynamicTextObservability.ResetForTests();
        DummyPopupShow.Reset();
        Translator.ResetForTests();
        MessageFrameTranslator.ResetForTests();
        MessagePatternTranslator.ResetForTests();
    }

    [TearDown]
    public void TearDown()
    {
        LocalizationAssetResolver.SetLocalizationRootForTests(null);
        Translator.ResetForTests();
        MessageFrameTranslator.ResetForTests();
        MessagePatternTranslator.ResetForTests();
    }

    [TestCase(
        nameof(DummyConversationRewardProducer.AddSlynthCandidateHandleEvent),
        "{{Y|Grit Gate}} is now a sanctuary option for the slynth.",
        "{{Y|Grit Gate}}がスリンスの聖域候補になった。",
        "SlynthSanctuary")]
    [TestCase(
        nameof(DummyConversationRewardProducer.AddSlynthCandidateHandleEvent),
        "{{Y|the salt dunes}} are now a sanctuary option for the slynth.",
        "{{Y|the salt dunes}}がスリンスの聖域候補になった。",
        "SlynthSanctuary")]
    [TestCase(
        nameof(DummyConversationRewardProducer.PaxInfectLimbInfectLimb),
        "You've contracted {{G|glowcrust}} on your left arm.",
        "left armに{{G|glowcrust}}を発症した。",
        "PaxInfectLimb")]
    [TestCase(
        nameof(DummyConversationRewardProducer.ReceiveItemHandleEvent),
        "You receive {{Y|an electrobow}} and {{C|three lead slugs}}!",
        "{{Y|an electrobow}} and {{C|three lead slugs}}を受け取った！",
        "ReceiveItem")]
    [TestCase(
        nameof(DummyConversationRewardProducer.LibrarianGiveBookHandleEvent),
        "The 司書 provides some insightful commentary on 'The Corpus Choliys'.",
        "司書は'The Corpus Choliys'について示唆に富む解説をしてくれた。",
        "LibrarianCommentary")]
    [TestCase(
        nameof(DummyConversationRewardProducer.LibrarianGiveBookHandleEvent),
        "You gain {{C|75}} XP.",
        "あなたは経験値を{{C|75}}獲得した",
        "ConversationXp")]
    [TestCase(
        nameof(DummyConversationRewardProducer.GiveReshephSecretHandleEvent),
        "You muse over the secret with {{G|Tszappur}} and gain some insight.",
        "{{G|Tszappur}}と秘密について思索し、いくらかの洞察を得た。",
        "ReshephSecretInsight")]
    [TestCase(
        nameof(DummyConversationRewardProducer.GiveReshephSecretHandleEvent),
        "You muse over the secrets with the Earl of Omonporch and gain some insight.",
        "the Earl of Omonporchと秘密について思索し、いくらかの洞察を得た。",
        "ReshephSecretInsight")]
    [TestCase(
        nameof(DummyConversationRewardProducer.GiveReshephSecretHandleEvent),
        "You gain {{C|325}} XP.",
        "あなたは経験値を{{C|325}}獲得した",
        "ConversationXp")]
    public void Patch_TranslatesConversationRewardPopups_WhenOwnerPatched(
        string methodName,
        string source,
        string expected,
        string detail)
    {
        if (methodName == nameof(DummyConversationRewardProducer.LibrarianGiveBookHandleEvent))
        {
            UseRepositoryPatternDictionary();
            UseRepositoryMessageFrames();
        }
        else if (detail == "ConversationXp")
        {
            UseRepositoryPatternDictionary();
        }

        RunWithOwnerAndPopupPatches(methodName, () =>
        {
            var target = new DummyConversationRewardProducer
            {
                PopupMessageToShow = source,
            };

            InvokeOwner(target, methodName);

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
                Assert.That(HitCount(detail), Is.EqualTo(1));
            });
        });
    }

    [Test]
    public void Patch_TranslatesMarkedLibrarianCommentary_WhenOwnerPatched()
    {
        const string visiblePrefix = "The 司書 provides";
        var source = DoesVerbRouteTranslator.MarkDoesFragment(visiblePrefix, "provide", "The 司書".Length, null)
            + " some insightful commentary on 'The Corpus Choliys'.";

        UseRepositoryMessageFrames();
        UseRepositoryPatternDictionary();

        RunWithOwnerAndPopupPatches(nameof(DummyConversationRewardProducer.LibrarianGiveBookHandleEvent), () =>
        {
            var target = new DummyConversationRewardProducer
            {
                PopupMessageToShow = source,
            };

            target.LibrarianGiveBookHandleEvent();

            Assert.Multiple(() =>
            {
                Assert.That(
                    DummyPopupShow.LastShowMessage,
                    Is.EqualTo("司書は'The Corpus Choliys'について示唆に富む解説をしてくれた。"));
                Assert.That(HitCount("LibrarianCommentary"), Is.EqualTo(1));
            });
        });
    }

    [Test]
    public void Patch_DoesNotTranslatePopupOnlyTraffic_WhenOwnerAbsent()
    {
        const string source = "You receive {{Y|an electrobow}} and {{C|three lead slugs}}!";

        OwnerPopupRouteTestHarness.WithPatchedPopupOnly(
            () => DummyPopupShow.Show(source));

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(source));
            Assert.That(HitCount("ReceiveItem"), Is.Zero);
        });
    }

    [Test]
    public void Patch_DoesNotRetranslateDirectMarkedPopup_WhenOwnerPatched()
    {
        const string source = "You've contracted {{G|glowcrust}} on your left arm.";
        var marked = MessageFrameTranslator.MarkDirectTranslation(source);

        RunWithOwnerAndPopupPatches(nameof(DummyConversationRewardProducer.PaxInfectLimbInfectLimb), () =>
        {
            var target = new DummyConversationRewardProducer
            {
                PopupMessageToShow = marked,
            };

            target.PaxInfectLimbInfectLimb();

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(source));
                Assert.That(HitCount("PaxInfectLimb"), Is.Zero);
            });
        });
    }

    [Test]
    public void Patch_LeavesUnknownDirectMarkedPopupUnchanged_WhenOwnerPatched()
    {
        const string source = "The conversation ends.";
        var marked = MessageFrameTranslator.MarkDirectTranslation(source);

        RunWithOwnerAndPopupPatches(nameof(DummyConversationRewardProducer.PaxInfectLimbInfectLimb), () =>
        {
            var target = new DummyConversationRewardProducer
            {
                PopupMessageToShow = marked,
            };

            target.PaxInfectLimbInfectLimb();

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(source));
                Assert.That(HitCount("PaxInfectLimb"), Is.Zero);
            });
        });
    }

    [Test]
    public void Patch_LeavesEmptyPopupUnchanged_WhenOwnerPatched()
    {
        RunWithOwnerAndPopupPatches(nameof(DummyConversationRewardProducer.ReceiveItemHandleEvent), () =>
        {
            var target = new DummyConversationRewardProducer
            {
                PopupMessageToShow = string.Empty,
            };

            target.ReceiveItemHandleEvent();

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(string.Empty));
                Assert.That(HitCount("ReceiveItem"), Is.Zero);
            });
        });
    }

    [TestCase("The conversation ends.")]
    public void Patch_LeavesUnsupportedPopupUnchanged_WhenOwnerPatched(string source)
    {
        RunWithOwnerAndPopupPatches(nameof(DummyConversationRewardProducer.ReceiveItemHandleEvent), () =>
        {
            var target = new DummyConversationRewardProducer
            {
                PopupMessageToShow = source,
            };

            target.ReceiveItemHandleEvent();

            Assert.Multiple(() =>
            {
                Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(source));
                Assert.That(HitCount("ReceiveItem"), Is.Zero);
                Assert.That(HitCount("ReshephSecretInsight"), Is.Zero);
                Assert.That(HitCount("ConversationXp"), Is.Zero);
            });
        });
    }

    [Test]
    public void Patch_RecordsRepositoryDictionaryFallbackAsPopupExact_NotConversationRewardOwner()
    {
        const string source = "You do not have any unshared secrets about the life of Resheph.";
        const string expected = "レシェフの生涯に関する未共有の秘密はない。";

        Translator.SetDictionaryDirectoryForTests(GetRepositoryDictionaryDirectory());

        OwnerPopupRouteTestHarness.WithPatchedPopupOnly(
            () => DummyPopupShow.Show(source));

        Assert.Multiple(() =>
        {
            Assert.That(DummyPopupShow.LastShowMessage, Is.EqualTo(expected));
            Assert.That(HitCount("ReshephSecretInsight"), Is.Zero);
            Assert.That(
                DynamicTextObservability.GetRouteFamilyHitCountForTests(
                    nameof(PopupShowTranslationPatch),
                    "Popup.ProducerText.Exact"),
                Is.EqualTo(1));
        });

        DynamicTextObservability.ResetForTests();
        DummyPopupShow.Reset();

        RunWithOwnerAndPopupPatches(nameof(DummyConversationRewardProducer.GiveReshephSecretHandleEvent), () =>
        {
            var target = new DummyConversationRewardProducer
            {
                PopupMessageToShow = source,
            };

            target.GiveReshephSecretHandleEvent();

            Assert.Multiple(() =>
            {
                Assert.That(
                    DummyPopupShow.LastShowMessage,
                    Is.EqualTo(expected));
                Assert.That(HitCount("ReshephSecretInsight"), Is.Zero);
                Assert.That(
                    DynamicTextObservability.GetRouteFamilyHitCountForTests(
                        nameof(PopupShowTranslationPatch),
                        "Popup.ProducerText.Exact"),
                    Is.EqualTo(1));
            });
        });
    }

    private static void RunWithOwnerAndPopupPatches(string methodName, Action action)
    {
        OwnerPopupRouteTestHarness.WithPatchedPopupOwner(
            typeof(ConversationRewardPopupTranslationPatch),
            OwnerPopupRouteTestHarness.RequireMethod(typeof(DummyConversationRewardProducer), methodName),
            action);
    }

    private static void InvokeOwner(DummyConversationRewardProducer target, string methodName)
    {
        _ = OwnerPopupRouteTestHarness.RequireMethod(typeof(DummyConversationRewardProducer), methodName).Invoke(target, null);
    }

    private static int HitCount(string detail)
    {
        return OwnerPopupRouteTestHarness.RouteHitCount(typeof(ConversationRewardPopupTranslationPatch), detail);
    }

    private sealed class DummyConversationRewardProducer
    {
        public string PopupMessageToShow = string.Empty;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public bool AddSlynthCandidateHandleEvent()
        {
            return EmitPopup(nameof(AddSlynthCandidateHandleEvent));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public bool PaxInfectLimbInfectLimb()
        {
            return EmitPopup(nameof(PaxInfectLimbInfectLimb));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public bool ReceiveItemHandleEvent()
        {
            return EmitPopup(nameof(ReceiveItemHandleEvent));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public bool LibrarianGiveBookHandleEvent()
        {
            return EmitPopup(nameof(LibrarianGiveBookHandleEvent));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public bool GiveReshephSecretHandleEvent()
        {
            return EmitPopup(nameof(GiveReshephSecretHandleEvent));
        }

        private bool EmitPopup(string route)
        {
            _ = route;
            DummyPopupShow.Show(PopupMessageToShow);
            return true;
        }
    }

    private static void UseRepositoryPatternDictionary()
    {
        var localizationRoot = Path.Combine(TestProjectPaths.GetRepositoryRoot(), "Mods", "QudJP", "Localization");
        LocalizationAssetResolver.SetLocalizationRootForTests(localizationRoot);
        MessagePatternTranslator.SetPatternFileForTests(null);
    }

    private static void UseRepositoryMessageFrames()
    {
        MessageFrameTranslator.SetDictionaryPathForTests(
            Path.Combine(
                TestProjectPaths.GetRepositoryRoot(),
                "Mods",
                "QudJP",
                "Localization",
                "MessageFrames",
                "verbs.ja.json"));
    }

    private static string GetRepositoryDictionaryDirectory() =>
        Path.Combine(
            TestProjectPaths.GetRepositoryRoot(),
            "Mods",
            "QudJP",
            "Localization",
            "Dictionaries");
}
